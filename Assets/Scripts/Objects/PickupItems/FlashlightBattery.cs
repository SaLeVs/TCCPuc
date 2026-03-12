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

        public bool CanInteract()
        {
            return true;
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (playerInteractor.TryGetComponent<Flashlight>(out Flashlight flashlight))
            {
                flashlight.IncreaseFlashlightBattery(batteryPercentRecharge);
                Destroy(gameObject);
                return true;
            }

            return false;
        }
    }
}

