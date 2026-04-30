using System;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class MissionOwnershipSelector : NetworkBehaviour
    {
        [SerializeField] private MissionSO mission;
        
        public MissionSO Mission => mission;
        public event Action<ulong> OnMissionOwnerAssigned;

        private NetworkVariable<ulong> _missionOwnerClientId = new NetworkVariable<ulong>(ulong.MaxValue);

        
        public override void OnNetworkSpawn()
        {
            _missionOwnerClientId.OnValueChanged += MissionManager_OnOwnerChanged;
        }

        private void MissionManager_OnOwnerChanged(ulong previousValue, ulong newValue)
        {
            OnMissionOwnerAssigned?.Invoke(newValue);
        }
        
        public void AssignOwner(ulong clientId)
        {
            if (IsServer)
            {
                _missionOwnerClientId.Value = clientId;
            }
        }

        public bool IsMissionOwner(ulong clientId)
        {
            return _missionOwnerClientId.Value == clientId;
        }
        
        
        public override void OnNetworkDespawn()
        {
            _missionOwnerClientId.OnValueChanged -= MissionManager_OnOwnerChanged;
        }
        
    }
}

