using Interfaces;
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

            UseBattery(playerInteractor);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef)
        {
            if (playerRef.TryGet(out NetworkObject playerNetworkObject))
            {
                UseBattery(playerNetworkObject.gameObject);
            }
        }

        private void UseBattery(GameObject playerInteractor)
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

