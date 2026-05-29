using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionOwnershipFilter : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> _managerRef = new();

        private MissionsManagerBase Manager
        {
            get
            {
                if (_managerRef.Value.TryGet(out NetworkObject networkObject))
                {
                    if (networkObject.TryGetComponent(out MissionsManagerBase missionManager))
                    {
                        return missionManager;
                    }
                }
                
                return null;
            }
        }

        public void SetManager(MissionsManagerBase manager)
        {
            if (!IsServer || manager == null || manager.NetworkObject == null) return;

            _managerRef.Value = manager.NetworkObject;
        }

        public bool CanClientInteract(ulong clientId)
        {
            MissionsManagerBase manager = Manager;

            if (manager == null) return false;

            MissionOwnershipSelector selector = manager.OwnershipSelector;

            if (selector == null) return false;
            
            return selector.IsMissionOwner(clientId);
        }
        
    }
}