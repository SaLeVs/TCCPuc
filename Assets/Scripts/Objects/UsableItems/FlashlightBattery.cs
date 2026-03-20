using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Objects.UsableItems
{
    public class FlashlightBattery : NetworkBehaviour, IUsable
    {
        [SerializeField] private int batteryPercentRecharge = 50;
        
        [Rpc(SendTo.ClientsAndHost)]
        

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
        
        private void DisableBatteryClientRpc()
        {
            gameObject.SetActive(false);
        }
    }  
}

