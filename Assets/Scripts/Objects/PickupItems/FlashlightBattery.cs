using Interfaces;
using ScriptableObjects;
using Systems;
using Unity.Netcode;
using UnityEngine;

namespace Objects.PickupItems
{
    public class FlashlightBattery : NetworkBehaviour
    {
        [SerializeField] private ItemDataSO itemData;
        
    }
}

