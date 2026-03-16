using System;
using Inputs;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Objects
{
    public class InventorySlot : MonoBehaviour
    {
        public event Action<int, ItemDataSO> OnUseRequested;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Image itemIcon;
        
        public int SlotIndex { get; private set; }
        
        private ItemDataSO _currentItem;

        
        private void OnEnable()
        {
            inputReader.OnSlotEvent += InputReader_OnSlotEvent;
        }

        
        private void InputReader_OnSlotEvent(int slotNumberPressed)
        {
            if ((slotNumberPressed - 1) == SlotIndex)
            {
                RequestUseItem();
            }
            
        }

        private void RequestUseItem()
        {
            OnUseRequested?.Invoke(SlotIndex, _currentItem);
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
            _currentItem = itemData;
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