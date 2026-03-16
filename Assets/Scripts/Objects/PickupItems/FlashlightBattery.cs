using Interfaces;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Objects.PickupItems
{
    public class FlashlightBattery : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDataSO itemData;
        [SerializeField] private int batteryPercentRecharge = 50;
        

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }
        
        public bool Interact(GameObject playerInteractor)
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

        public void UseBattery(GameObject playerInteractor)
        {
            if (playerInteractor.GetComponentInChildren<Flashlight>() is Flashlight flashlight)
            {
                flashlight.IncreaseFlashlightBattery(batteryPercentRecharge);
                DisableBatteryClientRpc();
            }
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        private void DisableBatteryClientRpc()
        {
            gameObject.SetActive(false);
        }
        
    }
}

