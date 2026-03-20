using Interfaces;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Objects.PickupItems
{
    public class ItemPickable : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDataSO itemData;

        

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }
        
        public bool Interact(GameObject playerInteractor)
        {
            if(CanInteract(playerInteractor))
            {
                NetworkObject networkObject = playerInteractor.GetComponent<NetworkObject>();
            
                if (!IsServer)
                {
                    InteractServerRpc(networkObject);
                    return true;
                }

                AddItemToInventory(playerInteractor);
                return true;
            }
            
            return false;
            
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef)
        {
            if (playerRef.TryGet(out NetworkObject playerNetworkObject))
            {
                AddItemToInventory(playerNetworkObject.gameObject);
            }
        }
        
        private void AddItemToInventory(GameObject playerInteractor)
        {
            PlayerInventory inventory = playerInteractor.GetComponent<PlayerInventory>();

            if (inventory == null) return;
            if (!inventory.HasInventorySpace()) return;

            inventory.TryAddItemServer(itemData.itemId);

            DisableBatteryClientRpc();
        }

        
        
        [Rpc(SendTo.ClientsAndHost)]
        private void DisableBatteryClientRpc()
        {
            gameObject.SetActive(false);
        }
        
    }
}

