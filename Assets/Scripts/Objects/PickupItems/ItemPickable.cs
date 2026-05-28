using Interfaces;
using Missions;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Objects.PickupItems
{
    public class ItemPickable : NetworkBehaviour, IInteractable, IMissionOwnerAware
    {
        [SerializeField] private ItemDataSO itemData;
        [SerializeField] private MissionOwnershipFilter ownershipFilter;

        public void SetOwnershipSelector(MissionsManagerBase manager)
        {
            ownershipFilter?.SetManager(manager);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;
            if (ownershipFilter == null) return true;

            return ownershipFilter.CanClientInteract(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out NetworkObject networkObject)) return false;

            if (!IsServer)
            {
                InteractServerRpc(networkObject);
                return true;
            }

            TryInteract(networkObject);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out NetworkObject playerNetworkObject)) return;

            TryInteract(playerNetworkObject);
        }

        private void TryInteract(NetworkObject playerNetworkObject)
        {
            if (ownershipFilter != null && !ownershipFilter.CanClientInteract(playerNetworkObject.OwnerClientId))
            {
                Debug.Log($"Interaction denied. ClientId: {playerNetworkObject.OwnerClientId}");
                return;
            }

            AddItemToInventory(playerNetworkObject.gameObject);
        }

        private void AddItemToInventory(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out PlayerInventory playerInventory))
                return;

            if (playerInventory.HasInventorySpace())
            {
                playerInventory.TryAddItemServer(itemData.itemId);
                RemoveItem();
                return;
            }

            if (playerInventory.CurrentSelectedSlot > 0)
            {
                playerInventory.ReplaceSelectedItemServer(
                    itemData.itemId,
                    transform.position,
                    transform.rotation
                );

                RemoveItem();
            }
        }

        private void RemoveItem()
        {
            if (!IsServer)
                return;

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }
}