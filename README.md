# UnityClientServerReplication
Unity with a basic client&lt;->server replication where the server is the authority.
There is no support for server reconciliation, only client side. This is a *naive* approach to basic server authority, works well for maybe a 2 player game or something casual.

1) Create a Unity project
2) Create an empty game object called "Manager"
3) Attach the Manager.cs script to it, tweak "Tickrate" to anything you want (eg 10-60)
4) Create a cube or other 3d object called "p1" (Case sensitive)
5) Attach a CharacterController component
6) On Manager, enable "IsServer".
7) Build, save, and run the executable somewhere.
8) On Manager, disable "IsServer".
9) Tweak "Client Prediction" and "Reconciliation" as you want, set "Client Latency" to anything you want (eg 20-1000)
10) Play! Use WASD to move. (You may need to configure these input axis in your project)

Bonus:
Numberbox can be a script you attach to an InputField that is strictly for numbers. Add an event to change the Number property, and it will automatically tweak either the tickrate or lag emulator depending on client or server running.
