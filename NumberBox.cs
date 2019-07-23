using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NumberBox : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        UnityEngine.UI.InputField field = GetComponent<InputField>();
        if (Manager.Instance.IsServer)
        {
            field.text = $"{Manager.Instance.ServerTickRate}";
        }
        else
        {
            field.text = $"{Manager.Instance.ClientLatency}";
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public string Number
    {
        get { return m_Number; }
        set
        {
            m_Number = value;

            if (!String.IsNullOrEmpty(value))
            {
                if (Manager.Instance.IsServer)
                {
                    Manager.Instance.ServerTickRate = int.Parse(m_Number);
                }
                else
                {
                    Manager.Instance.ClientLatency = int.Parse(m_Number);
                }
            }
        }
    }

    [SerializeField]
    private string m_Number;
}