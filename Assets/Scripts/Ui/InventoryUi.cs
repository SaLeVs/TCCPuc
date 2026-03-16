using Unity.Netcode;
using UnityEngine;
using Player;
using ScriptableObjects;

namespace UI
{
    public class InventoryUi : NetworkBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private ItemListSO itemDatabase;
        [SerializeField] private Transform inventoryHolder;
        [SerializeField] private GameObject inventorySlotPrefab;
        
        private int _maxInventorySize;
        
        
        public override void OnNetworkSpawn()
        {
            inventory.OnSlotChanged += Inventory_OnSlotChanged;
            _maxInventorySize = inventory.MaxInventorySize;
            
        }

        private void Inventory_OnSlotChanged(int slotIndex, int itemId)
        {
            ItemDataSO item = itemDatabase.GetItem(itemId);
            if (item == null) return;
            Debug.Log($"Item {item.itemName} added to slot {slotIndex}");
            AddItemToSlot(slotIndex, item);
            
        }

        private void AddItemToSlot(int slotIndex, ItemDataSO item)
        {
            
                
        }

        public override void OnNetworkDespawn()
        {
            inventory.OnSlotChanged -= Inventory_OnSlotChanged;
        }
    }
}