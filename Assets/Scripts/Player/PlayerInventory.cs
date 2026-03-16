using System;
using Inputs;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerInventory : NetworkBehaviour
    {
        public event Action<int,int> OnSlotChanged;
        public event Action<int, int> OnCreateSlot;
        public event Action<int, int> OnRemoveSlot;
        
        [SerializeField] private int maxInventorySize = 4;
        [SerializeField] private ItemListSO itemDatabase;
        [SerializeField] private InputReader inputReader;
        
        public int MaxInventorySize => maxInventorySize; 
        
        private NetworkList<int> _slots = new NetworkList<int>();
        private int _currentSlotSelected;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _slots.Clear();
                
                for(int i = 0; i < maxInventorySize; i++)
                {
                    _slots.Add(-1);
                    
                }
            }
            
            _slots.OnListChanged += Slots_OnListChanged;

            if (IsOwner)
            {
                inputReader.OnSlotEvent += InputReader_OnSlotEvent;
                inputReader.OnUseEvent += InputReader_OnUseEvent;
            }
            
        }

        private void InputReader_OnSlotEvent(int slotSelected)
        {
            Debug.Log($"Slot {slotSelected} selected");
        }

        private void InputReader_OnUseEvent()
        {
            Debug.Log($"Use item in slot {_currentSlotSelected}");
        }


        private void Slots_OnListChanged(NetworkListEvent<int> change)
        {
            OnSlotChanged?.Invoke(change.Index, change.Value);
            Debug.Log($"Slot {change.Index} changed to {change.Value}");
        }
        
        public void TryAddItemServer(int itemId)
        {
            if (!IsServer) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == -1)
                {
                    _slots[i] = itemId;
                    Debug.Log($"Item {itemId} added to slot {i}");
                    return;
                }
            }
            
        }

        public bool HasInventorySpace()
        {
            return _slots.Contains(-1);
        }

        public void RemoveItem(int itemId)
        {
            
        }
        
    }
}


