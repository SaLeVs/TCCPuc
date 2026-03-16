using Inputs;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Objects
{
    public class InventorySlot : NetworkBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private InputReader inputReader;
        
        public int SlotIndex { get; private set; }


        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.
            }
        }
        
        public void Init(int index)
        {
            SlotIndex = index;
            Clear();
        }

        public void SetItem(ItemDataSO itemData)
        {
            icon.sprite = itemData.icon;
            icon.SetEnabled(true);
        }
        
        public void Clear()
        {
            icon.sprite = null;
            icon.SetEnabled(false);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                
            }
        }
        
    }
}