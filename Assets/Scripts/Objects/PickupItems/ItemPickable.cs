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
            if (playerInteractor.TryGetComponent(out PlayerInventory playerInventory))
            {
                if (playerInventory.HasInventorySpace())
                {
                    playerInventory.TryAddItemServer(itemData.itemId);
                    DisableItemClientRpc();
                    return;
                }

                if (playerInventory.CurrentSelectedSlot > 0)
                {
                    playerInventory.ReplaceSelectedItemServer(itemData.itemId, transform.position, transform.rotation);
                    DisableItemClientRpc();
                }
                
            }
        }
        
        
        [Rpc(SendTo.ClientsAndHost)]
        private void DisableItemClientRpc()
        {
            gameObject.SetActive(false);
        }
        
    }
}

