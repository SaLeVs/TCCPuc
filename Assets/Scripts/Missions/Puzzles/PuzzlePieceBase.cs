using Unity.Netcode;
using UnityEngine;

namespace Missions.Puzzles
{
    public abstract class PuzzlePieceBase : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> _managerRef = new();

        public abstract bool IsCorrect { get; }

        protected MissionOwnershipSelector OwnershipSelector => Manager?.OwnershipSelector;

        protected PuzzleManagerBase Manager
        {
            get
            {
                if (_managerRef.Value.TryGet(out NetworkObject networkObject))
                {
                    if (networkObject.TryGetComponent(out PuzzleManagerBase manager))
                    {
                        return manager;
                    }
                }

                return null;
            }
        }

        // Chamado pelo PuzzleManagerBase.SpawnPiece.
        internal void Bind(PuzzleManagerBase manager)
        {
            if (!IsServer) return;

            _managerRef.Value = manager.NetworkObject;
        }

        protected void NotifyChanged(ulong clientId)
        {
            Manager?.NotifyPieceChanged(this, clientId);
        }

        protected bool CheckOwnership(ulong clientId)
        {
            if (Manager == null)
            {
                Debug.LogWarning($"{name}: Manager is null! _managerRef synced?");
                return false;
            }

            if (Manager.IsComplete) return false;

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
