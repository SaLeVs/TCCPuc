using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class PlayerDiskHolder : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _diskCount = new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool HasDisk => _diskCount.Value > 0;

        public void GiveDisk()
        {
            if (!IsServer) return;
            _diskCount.Value++;
        }

        public bool TryConsumeDisk()
        {
            if (!IsServer) return false;
            if (!HasDisk) return false;
            
            _diskCount.Value--;
            return true;
        }
        
    }
}

