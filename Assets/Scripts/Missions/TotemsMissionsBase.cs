using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class TotemsMissionsBase : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> _managerRef = new();
        
        protected MissionOwnershipSelector OwnershipSelector => Manager?.OwnershipSelector;
        
        protected MissionsManagerBase Manager
        {
            get
            {
                if (_managerRef.Value.TryGet(out NetworkObject networkObject))
                {
                    if(networkObject.TryGetComponent(out MissionsManagerBase manager))
                    {
                        return manager;
                    }
                }
                
                return null;
            }
        }
        
        protected void InitializeBase(MissionsManagerBase missionManager)
        {
            if (!IsServer) return;
            
            _managerRef.Value = missionManager.NetworkObject;
        }

        protected bool CheckOwnership(ulong clientId)
        {
            if (Manager == null || Manager.IsComplete) return false;

            MissionOwnershipSelector selector = OwnershipSelector;
            
            if (selector == null)
            {
                Debug.LogWarning($"{name}: OwnershipSelector not sync yet");
                return false;
            }

            return selector.IsMissionOwner(clientId);
        }
        
    }
}

