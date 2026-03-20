using Inputs;
using Interfaces;
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
                
                Transform playerRoot = transform.root;
                
                if(playerRoot.TryGetComponent(out GameObject player))
                {
                    _playerInteractor = player;
                }
            }
        }

        private void InputReader_OnUseEvent()
        {
            Use(_playerInteractor);
        }

        public bool CanUse(GameObject playerInteractor)
        {
            return true;
        }

        public void Use(GameObject playerInteractor)
        {
            if(CanUse(playerInteractor))
            {
                if (playerInteractor.GetComponentInChildren<Flashlight>() is Flashlight flashlight)
                {
                    flashlight.IncreaseFlashlightBattery(batteryPercentRecharge);
                    DisableBatteryClientRpc();
                }
            }
            
        }
        
        
        [Rpc(SendTo.ClientsAndHost)]
        private void DisableBatteryClientRpc()
        {
            gameObject.SetActive(false);
        }
    }  
}

