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
        
        private MissionOwnershipFilter _ownershipFilter;

        public int ItemId => itemData.itemId;
        
        private void Awake()
        {
            _ownershipFilter = GetComponent<MissionOwnershipFilter>();
        }
        

        public void SetOwnershipSelector(MissionsManagerBase manager)
        {
            _ownershipFilter?.SetManager(manager);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;
            if (_ownershipFilter == null) return true;

            return _ownershipFilter.CanClientInteract(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;
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
            if (_ownershipFilter != null && !_ownershipFilter.CanClientInteract(playerNetworkObject.OwnerClientId))
            {
                Debug.Log($"Interaction denied. ClientId: {playerNetworkObject.OwnerClientId}");
                return;
            }

            AddItemToInventory(playerNetworkObject.gameObject);
        }

        private void AddItemToInventory(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out PlayerInventory playerInventory)) return;

            if (playerInventory.HasInventorySpace())
            {
                playerInventory.TryAddItemServer(itemData.itemId);
                RemoveItem();
                return;
            }

            if (playerInventory.CurrentSelectedSlot > 0)
            {
                playerInventory.ReplaceSelectedItemServer(itemData.itemId, transform.position, transform.rotation);
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