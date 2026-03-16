using Objects;
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
        
        private InventorySlot[] _slotUIs;
        private int _maxInventorySize;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inventory.OnSlotChanged += Inventory_OnSlotChanged;
                _maxInventorySize = inventory.MaxInventorySize;
                CreateInventorySlots();
            }
            
        }
        
        private void CreateInventorySlots()
        {
            _slotUIs = new InventorySlot[_maxInventorySize];

            for (int i = 0; i < _maxInventorySize; i++)
            {
                GameObject slotUi = Instantiate(inventorySlotPrefab, inventoryHolder);

                if (slotUi.TryGetComponent(out InventorySlot slot))
                {
                    slot.Init(i);
                    slot.Clear();
                    _slotUIs[i] = slot;
                }
            }
        }
        
        private void Inventory_OnSlotChanged(int slotIndex, int itemId)
        {
            if (itemId == -1)
            {
                _slotUIs[slotIndex].Clear();
                return;
            }
            
            ItemDataSO item = itemDatabase.GetItem(itemId);
            _slotUIs[slotIndex].SetItem(item);

            Debug.Log($"Item {item.itemName} added to slot {_slotUIs[slotIndex].SlotIndex}");
        }

        public override void OnNetworkDespawn()
        {
            inventory.OnSlotChanged -= Inventory_OnSlotChanged;
        }
    }
}