using System;
using Inputs;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Objects
{
    public class InventorySlot : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private GameObject highlightSlot;

        public int SlotIndex { get; private set; }

        
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

        public void SetHighlight(bool value)
        {
            highlightSlot.SetActive(value);
        }

        
    }
}