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
        [SerializeField] private GameObject highlightSlot;
        
        public int SlotIndex { get; private set; }
        
        private ItemDataSO _currentItem;
        private InventorySlot _currentInventorySlotSelected;
        
        
        private void OnEnable()
        {
            inputReader.OnSlotEvent += InputReader_OnSlotEvent;
            inputReader.OnUseEvent += InputReader_OnUseEvent;
        }
        
        private void InputReader_OnSlotEvent(int slotNumberPressed)
        {
            if ((slotNumberPressed - 1) == SlotIndex)
            {
                SelectSlot();
            }
            
        }

        private void SelectSlot()
        {
            if (_currentInventorySlotSelected != null)
            {
                _currentInventorySlotSelected.highlightSlot.SetActive(false);
            }
            
            _currentInventorySlotSelected = this;
            highlightSlot.SetActive(true);
            Debug.Log($"Slot {SlotIndex} selected");
        }
        
        private void InputReader_OnUseEvent()
        {
            OnUseRequested?.Invoke(SlotIndex, _currentItem);
            Debug.Log($"Use requested for slot {SlotIndex}");
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
            inputReader.OnUseEvent -= InputReader_OnUseEvent;
        }

        
    }
}