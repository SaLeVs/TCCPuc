using Inputs;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Objects
{
    public class InventorySlot : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Image itemIcon;
        
        public int SlotIndex { get; private set; }


        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnSlotEvent += InputReader_OnSlotEvent;
            }
            
        }

        private void InputReader_OnSlotEvent(int slotNumberPressed)
        {
            Debug.Log($"SlotNumberPressed: {slotNumberPressed}, SlotIndex {SlotIndex} pressed");
            
            if (slotNumberPressed == SlotIndex)
            {
                Debug.Log("Slot correct");
            }
            
        }

        public void Init(int index)
        {
            SlotIndex = index;
            Clear();
        }

        public void SetItem(ItemDataSO itemData)
        {
            itemIcon.sprite = itemData.icon;
            itemIcon.enabled = true;
        }
        
        public void Clear()
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }

        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnSlotEvent -= InputReader_OnSlotEvent;
            }
            
        }
        
    }
}