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
            Debug.Log($"Selector Spawned. Current Owner: {_missionOwnerClientId.Value}");
            _missionOwnerClientId.OnValueChanged += MissionManager_OnOwnerChanged;
            
            if (_missionOwnerClientId.Value != ulong.MaxValue)
            {
                OnMissionOwnerAssigned?.Invoke(_missionOwnerClientId.Value);
            }
        }

        private void MissionManager_OnOwnerChanged(ulong previousValue, ulong newValue)
        {
            Debug.Log(
                $"Owner changed on {name}. Previous: {previousValue} New: {newValue}"
            );
            
            OnMissionOwnerAssigned?.Invoke(newValue);
        }
        
        public void AssignOwner(ulong clientId)
        {
            if (!IsServer) return;

            Debug.Log(
                $"Assigning owner {clientId} to selector {name}"
            );

            _missionOwnerClientId.Value = clientId;

            Debug.Log(
                $"Current owner after assign: {_missionOwnerClientId.Value}"
            );
        }

        public bool IsMissionOwner(ulong clientId)
        {
            if (_missionOwnerClientId.Value == ulong.MaxValue)
            {
                Debug.LogWarning("Mission owner not assigned yet.");
                return false;
            }

            return _missionOwnerClientId.Value == clientId;
        }
        
        
        public override void OnNetworkDespawn()
        {
            _missionOwnerClientId.OnValueChanged -= MissionManager_OnOwnerChanged;
        }
        
    }
}

