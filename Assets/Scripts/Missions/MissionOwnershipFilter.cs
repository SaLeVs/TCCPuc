using Missions.Puzzles;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionOwnershipFilter : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> _managerRef = new();

        private PuzzleManagerBase Manager
        {
            get
            {
                if (_managerRef.Value.TryGet(out NetworkObject networkObject))
                {
                    if (networkObject.TryGetComponent(out PuzzleManagerBase missionManager))
                    {
                        return missionManager;
                    }
                }
                
                return null;
            }
        }

        public void SetManager(PuzzleManagerBase manager)
        {
            if (!IsServer || manager == null || manager.NetworkObject == null) return;

            _managerRef.Value = manager.NetworkObject;
        }

        public bool CanClientInteract(ulong clientId)
        {
            PuzzleManagerBase manager = Manager;

            if (manager == null) return false;

            // A solved puzzle is finished with: its pieces already refuse to change
            // (PuzzlePieceBase.CheckOwnership), and without this the boxes sitting on the totems
            // could still be picked back off them once the mission was complete.
            if (manager.IsComplete) return false;

            MissionOwnershipSelector selector = manager.OwnershipSelector;

            if (selector == null) return false;
            
            return selector.IsMissionOwner(clientId);
        }
        
    }
}