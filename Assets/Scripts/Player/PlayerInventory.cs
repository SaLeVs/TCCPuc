using System;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerInventory : NetworkBehaviour
    {
        public event Action<int,int> OnSlotChanged;
        
        [SerializeField] private int maxInventorySize = 4;
        
        public int MaxInventorySize => maxInventorySize;
        
        private NetworkList<int> _slots = new NetworkList<int>();
        
        
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
            
        }
        
        
        private void Slots_OnListChanged(NetworkListEvent<int> change)
        {
            OnSlotChanged?.Invoke(change.Index, change.Value);
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


