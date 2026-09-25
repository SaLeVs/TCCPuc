using Interfaces;
using Unity.Netcode;
using UnityEngine;
using ScriptableObjects;

namespace Missions.Puzzles
{
    public class ItemSlotTotem : PuzzlePieceBase, IInteractable
    {
        [SerializeField] private ItemDataSO expectedItem;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private ItemListSO itemDatabase;

        public override bool IsCorrect => HasItemInSlot() && _currentItemId == expectedItem.itemId;

        private NetworkObject _currentPickable;
        private int _currentItemId = -1;


        public bool CanInteract(GameObject interactor)
        {
            if (HasItemInSlot()) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;

            return CheckOwnership(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor) => false;

        public bool TryDeposit(ulong clientId, int itemId)
        {
            if (!CheckOwnership(clientId)) return false;
            if (HasItemInSlot()) return false;

            ItemDataSO item = itemDatabase.GetItem(itemId);

            if (item == null) return false;

            GameObject spawned = Instantiate(item.prefabPickable, spawnPoint.position, spawnPoint.rotation);

            if (spawned.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
                _currentPickable = netObj;

                if (spawned.TryGetComponent(out IMissionOwnerAware ownerAware))
                {
                    ownerAware.BindToPuzzle(Manager);
                }
            }

            _currentItemId = itemId;
            NotifyChanged(clientId);
            return true;
        }

        private bool HasItemInSlot()
        {
            if (_currentPickable == null) return false;

            if (!_currentPickable || !_currentPickable.IsSpawned)
            {
                ClearSlot();
                return false;
            }

            return true;
        }

        private void ClearSlot()
        {
            _currentPickable = null;
            _currentItemId = -1;
        }

    }
}
