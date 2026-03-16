using Inputs;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Objects
{
    public class InventorySlot : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Image itemIcon;
        
        public int SlotIndex { get; private set; }


        private void OnEnable()
        {
            inputReader.OnSlotEvent += InputReader_OnSlotEvent;
        }

        
        private void InputReader_OnSlotEvent(int slotNumberPressed)
        {
            if ((slotNumberPressed - 1) == SlotIndex)
            {
                Debug.Log($"Slot correct {SlotIndex} pressed");
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

        
        private void OnDisable()
        {
            inputReader.OnSlotEvent -= InputReader_OnSlotEvent;
        }

        
    }
}