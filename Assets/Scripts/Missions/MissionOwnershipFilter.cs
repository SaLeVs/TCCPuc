using Missions.PersonalMissions;
using Unity.Netcode;

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
            if (!IsServer) return;
            _managerRef.Value = manager.NetworkObject;
        }

        public bool CanClientInteract(ulong clientId)
        {
            if (Manager == null) return true;
            if (Manager.IsComplete) return false;

            MissionOwnershipSelector selector = Manager.OwnershipSelector;
            if (selector == null) return false;

            return selector.IsMissionOwner(clientId);
        }
        
    }
}
