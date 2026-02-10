using Unity.NetCode;
using UnityEngine;

namespace Network
{
    [UnityEngine.Scripting.Preserve]
    public class GameBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            AutoConnectPort = 7979; // Set the default port for auto-connecting clients in the editor
            return base.Initialize(defaultWorldName);
        }
    }
}

