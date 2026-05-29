using System;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Missions.PersonalMissions
{
    public class MissionOwnershipSelector : NetworkBehaviour
    {
        public event Action<ulong> OnMissionOwnerAssigned;
        
        [SerializeField] private MissionSO mission;
        
        public MissionSO Mission => mission;
        public bool IsMissionOwnerAssigned => _missionOwnerClientId.Value != ulong.MaxValue;
        public ulong OwnerClientIdSelector => _missionOwnerClientId.Value;
        
        private NetworkVariable<ulong> _missionOwnerClientId = new NetworkVariable<ulong>(ulong.MaxValue);

        
        public override void OnNetworkSpawn()
        {
            _missionOwnerClientId.OnValueChanged += MissionManager_OnOwnerChanged;
            
            if (_missionOwnerClientId.Value != ulong.MaxValue)
            {
                OnMissionOwnerAssigned?.Invoke(_missionOwnerClientId.Value);
            }
        }

        private void MissionManager_OnOwnerChanged(ulong previousValue, ulong newValue)
        {
            OnMissionOwnerAssigned?.Invoke(newValue);
        }
        
        public void AssignOwner(ulong clientId)
        {
            if (!IsServer) return;

            _missionOwnerClientId.Value = clientId;
        }

        public bool IsMissionOwner(ulong clientId)
        {
            if (_missionOwnerClientId.Value == ulong.MaxValue) return false;

            return _missionOwnerClientId.Value == clientId;
        }
        
        
        public override void OnNetworkDespawn()
        {
            _missionOwnerClientId.OnValueChanged -= MissionManager_OnOwnerChanged;
        }
        
    }
}

