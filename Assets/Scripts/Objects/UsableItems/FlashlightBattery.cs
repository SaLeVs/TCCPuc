using Inputs;
using Interfaces;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace Objects.UsableItems
{
    public class FlashlightBattery : NetworkBehaviour, IUsable
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private int batteryPercentRecharge = 50;

        private GameObject _playerInteractor;
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnUseEvent += InputReader_OnUseEvent;
                
                NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);

                if (playerNetworkObject != null)
                {
                    _playerInteractor = playerNetworkObject.gameObject;
                }
            }
        }

        private void InputReader_OnUseEvent()
        {
            Use(_playerInteractor);
        }

        public bool CanUse(GameObject playerInteractor)
        {
            if (playerInteractor.TryGetComponent(out PlayerState playerState))
            {
                return !playerState.IsDead;
            }
            
            return true;
        }

        public void Use(GameObject playerInteractor)
        {
            if(CanUse(playerInteractor))
            {
                if (playerInteractor.GetComponentInChildren<Flashlight>() is Flashlight flashlight)
                {
                    flashlight.IncreaseFlashlightBattery(batteryPercentRecharge);
                    
                    if (playerInteractor.TryGetComponent(out PlayerInventory inventory))
                    {
                        inventory.TryRemoveItemServer();
                    }
                }
            }
            
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnUseEvent -= InputReader_OnUseEvent;
                _playerInteractor = null;
            }
        }
    }  
}

