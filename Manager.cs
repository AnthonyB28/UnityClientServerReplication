using System;
using System.Collections;
using System.Collections.Generic;
using BeardedManStudios.Forge.Networking;
using BeardedManStudios.Forge.Networking.Frame;
using BeardedManStudios.Forge.Networking.Unity;
using UnityEngine;

public class Manager : MonoBehaviour
{
    public static Manager Instance;


    public bool IsServer
    {
        get { return m_IsServer; }
        set { m_IsServer = value; }
    }

    [SerializeField]
    private bool m_IsServer = false;

    public bool ClientSidePrediction
    {
        get { return m_ClientSidePrediction; }
        set { m_ClientSidePrediction = value; }
    }

    [SerializeField]
    private bool m_ClientSidePrediction = false;

    public bool ServerReconcile
    {
        get { return m_ServerReconcile; }
        set { m_ServerReconcile = value; }
    }

    [SerializeField]
    private bool m_ServerReconcile = false;

    public bool Interpolation
    {
        get { return m_Interpolation; }
        set { m_Interpolation = value; }
    }

    [SerializeField]
    private bool m_Interpolation = false;

    public int ServerTickRate
    {
        get { return m_ServerTickRate; }
        set
        {
            m_ServerTickRate = value;
        }
    }

    [SerializeField]
    private int m_ServerTickRate = 10;

    public int ClientLatency
    {
        get { return m_ClientLatency; }
        set
        {
            m_ClientLatency = value;
        }
    }

    [SerializeField]
    private int m_ClientLatency = 100;

    // Bad code shared both on client & server.
    private readonly Queue<ServerPosition> m_ClientServerReceivedMessages = new Queue<ServerPosition>();
    private readonly Queue<Inputs> m_ServerClientReceivedMessages = new Queue<Inputs>();
    private readonly Queue<ClientPacket> m_ClientMessageToServerQueue = new Queue<ClientPacket>();
    private readonly List<Inputs> m_ClientPendingInputsToServer = new List<Inputs>();
    private UDPServer m_Server;
    private UDPClient m_Client;
    private GameObject m_Player1;
    private const string m_Ip = "127.0.0.1";
    private Vector3 m_Player1PrevPosition;
    private const ushort m_Port = 6700;
    private float m_TimeDelta = 0;
    private ushort m_UpdatesDone = 0;
    private ulong m_ServerLastProcessedInput;
    private ulong m_SeqNum = 0;

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this);
    }

    private void ClientOnBinaryMessageReceived(NetworkingPlayer player, Binary frame, NetWorker sender)
    {
        if (frame.GroupId == id)
        {
            Vector3 position = Vector3.zero;
            byte[] data = frame.GetData();
            position.x = BitConverter.ToSingle(data, 0 * sizeof(float));
            position.y = BitConverter.ToSingle(data, 1 * sizeof(float));
            position.z = BitConverter.ToSingle(data, 2 * sizeof(float));
            ulong lastSeqNum = BitConverter.ToUInt64(data, 3 * sizeof(float));
            m_ClientServerReceivedMessages.Enqueue(new ServerPosition() { LastProcessedSeqNum = lastSeqNum, Position = position});
        }
    }

    private void ServerOnBinaryMessageReceived(NetworkingPlayer player, Binary frame, NetWorker sender)
    {
        if (frame.GroupId == id)
        {
            byte[] data = frame.GetData();
            Inputs input = new Inputs();
            input.SeqNum = BitConverter.ToUInt64(data, 0);
            input.h = BitConverter.ToSingle(data, sizeof(ulong));
            input.v = BitConverter.ToSingle(data, sizeof(ulong) + sizeof(float));
            input.deltaTime = BitConverter.ToSingle(data, sizeof(ulong) + sizeof(float) + sizeof(float));
            m_ServerClientReceivedMessages.Enqueue(input);
        }
    }

    void Start()
    {
        if (IsServer)
        {
            m_Server = new UDPServer(2);
            m_Server.binaryMessageReceived += ServerOnBinaryMessageReceived;
            NetworkManager.Instance.Initialize(m_Server);
            m_Server.Connect(m_Ip, m_Port);
        }
        else
        {
            m_Client = new UDPClient();
            m_Client.binaryMessageReceived += ClientOnBinaryMessageReceived;
            NetworkManager.Instance.Initialize(m_Client);
            m_Client.Connect(m_Ip, m_Port);
        }

        m_Player1 = GameObject.Find("p1");
        m_Player1PrevPosition = m_Player1.transform.position;
    }

    void Update()
    {

    }

    private const int id = MessageGroupIds.START_OF_GENERIC_IDS + 1;
    void FixedUpdate()
    {
        m_TimeDelta += Time.fixedDeltaTime;
        if (IsServer)
        {
            if (m_TimeDelta >= 1.0f)
            {
                m_UpdatesDone = 0;
                m_TimeDelta = 0;
            }

            if (m_UpdatesDone > ServerTickRate)
            {
                return;
            }

            ServerProcessInputs();
            ServerSendWorldState();
            m_UpdatesDone++;
        }
        else if (!IsServer && m_Client.IsConnected)
        {
            ClientProcessServerMessages();
            ClientProcessInputs();
            ClientSendMessagesToServer();
            m_TimeDelta = 0;

            // Interpolate other movement here.
        }
        
    }

    private void ServerSendWorldState()
    {
        m_Player1PrevPosition = m_Player1.transform.position;
        byte[] buff = new byte[sizeof(float) * 3 + sizeof(ulong)];
        Buffer.BlockCopy(BitConverter.GetBytes(m_Player1.transform.position.x), 0, buff, 0 * sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(m_Player1.transform.position.y), 0, buff, 1 * sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(m_Player1.transform.position.z), 0, buff, 2 * sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(m_ServerLastProcessedInput), 0, buff, 3 * sizeof(float), sizeof(ulong));
        Binary bin = new Binary(NetworkManager.Instance.Networker.Time.Timestep, false, buff, Receivers.All, id,
            false);
        m_Server.Send(bin, false);
    }

    private void ServerProcessInputs()
    {
        while (m_ServerClientReceivedMessages.Count > 0)
        {
            Inputs i = m_ServerClientReceivedMessages.Dequeue();
            // validate input
            // Check if this is our entity, assuming it is for now.
            ApplyInput(i);
            m_ServerLastProcessedInput = i.SeqNum;
        }
    }

    private void ClientProcessInputs()
    {
        float inputh = Input.GetAxis("Horizontal");
        float inputv = Input.GetAxis("Vertical");
        if (inputv != 0 || inputh != 0)
        {
            Inputs i = new Inputs() { h = inputh, v = inputv, SeqNum = m_SeqNum++, deltaTime = Time.fixedDeltaTime };
            ClientSendInput(i);
            if (ClientSidePrediction)
            {
                ApplyInput(i);
            }

            m_ClientPendingInputsToServer.Add(i);
        }
    }

    private void ClientSendInput(Inputs i)
    {
        byte[] buff = new byte[sizeof(ulong)  + sizeof(float) + sizeof(float) + sizeof(float)];
        Buffer.BlockCopy(BitConverter.GetBytes(i.SeqNum), 0, buff, 0, sizeof(ulong));
        Buffer.BlockCopy(BitConverter.GetBytes(i.h), 0, buff, sizeof(ulong), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(i.v), 0, buff, sizeof(ulong) + sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(i.deltaTime), 0, buff, sizeof(ulong) + sizeof(float) + sizeof(float), sizeof(float));
        Binary bin = new Binary(NetworkManager.Instance.Networker.Time.Timestep, false, buff, Receivers.Server, id, false);
        m_ClientMessageToServerQueue.Enqueue(new ClientPacket() { WhenToSend = DateTime.Now.AddMilliseconds(ClientLatency), Payload = bin});
    }

    private void ClientSendMessagesToServer()
    {
        while (m_ClientMessageToServerQueue.Count > 0)
        {
            if (DateTime.Now >= m_ClientMessageToServerQueue.Peek().WhenToSend)
            {
                m_Client.Send(m_ClientMessageToServerQueue.Dequeue().Payload, false);
            }
            else
            {
                break;
            }
        }
    }

    private void ApplyInput(Inputs i)
    {
        CharacterController f = m_Player1.GetComponent<CharacterController>();
        f.Move(new Vector3(i.h * i.deltaTime * 4, 0.0f, i.v * i.deltaTime * 4));
    }

    private void ClientProcessServerMessages()
    {
        while (m_ClientServerReceivedMessages.Count > 0)
        {
            ServerPosition serverMessage = m_ClientServerReceivedMessages.Dequeue();

            // Hack??? Needed to get around setting position.
            m_Player1.GetComponent<CharacterController>().enabled = false;
            m_Player1.transform.position = serverMessage.Position;
            m_Player1.GetComponent<CharacterController>().enabled = true;
            if (ServerReconcile)
            {
                int i = 0;
                while(i < m_ClientPendingInputsToServer.Count)
                {
                    Inputs h = m_ClientPendingInputsToServer[i];
                    if (h.SeqNum <= serverMessage.LastProcessedSeqNum)
                    {
                        m_ClientPendingInputsToServer.RemoveAt(i);
                    }
                    else
                    {
                        // Not processed by server yet. Re-Apply.
                        ApplyInput(h);
                        i++;
                    }
                }
            }
            else
            {
                m_ClientPendingInputsToServer.Clear();
            }
        }
    }

    private class ServerPosition
    {
        public ulong LastProcessedSeqNum;
        public Vector3 Position;
    }

    private class Inputs
    {
        public ulong SeqNum;
        public float h;
        public float v;
        public float deltaTime;
    }

    private class ClientPacket
    {
        public DateTime WhenToSend;
        public Binary Payload;
    }

}
