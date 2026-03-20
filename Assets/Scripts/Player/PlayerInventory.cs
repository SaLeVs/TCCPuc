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
        public event Action<int> OnSelectedSlotChanged;
        
        [SerializeField] private int maxInventorySize = 4;
        [SerializeField] private ItemListSO itemDatabase;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform playerHand;
        
        public int MaxInventorySize => maxInventorySize; 
        public int CurrentSelectedSlot => _currentSlotSelected;
        
        private NetworkList<int> _slots = new NetworkList<int>();
        private int _currentSlotSelected;
        private int _currentItemId = -1;
        
        
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
            }
            
        }

        private void InputReader_OnSlotEvent(int slotSelected)
        {
            SelectSlot(slotSelected);
        }


        private void Slots_OnListChanged(NetworkListEvent<int> change)
        {
            OnSlotChanged?.Invoke(change.Index, change.Value);

            if (change.Value == -1)
            {
                OnRemoveSlot?.Invoke(change.Index, change.Value);
            }
            else
            {
                OnCreateSlot?.Invoke(change.Index, change.Value);
            }
            
            int selectedZeroBased = _currentSlotSelected - 1;
            
            if (_currentSlotSelected > 0 && change.Index == selectedZeroBased)
            {
                _currentItemId = change.Value;
            }
            
        }
        
        private void SelectSlot(int slotSelected)
        {
            if (_currentSlotSelected == slotSelected) return;

            _currentSlotSelected = slotSelected;
            int zeroIndex = _currentSlotSelected - 1;

            if (zeroIndex >= 0 && zeroIndex < _slots.Count)
            {
                _currentItemId = _slots[zeroIndex];
            }
            else
            {
                _currentItemId = -1;
            }
            
            OnSelectedSlotChanged?.Invoke(_currentSlotSelected);
            
            CreateItemRpc(_currentItemId);
            
            Debug.Log($"Selected slot {_currentSlotSelected}");
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
    
        [Rpc(SendTo.Server)]
        private void CreateItemRpc(int itemId)
        {
            if (IsServer)
            {
                ItemDataSO item = itemDatabase.GetItem(itemId);
                
                if (item != null)
                {
                    GameObject itemObject = Instantiate(item.prefabUsable, playerHand.position, playerHand.rotation);
                    itemObject.transform.SetParent(playerHand);
                    
                    if(itemObject.TryGetComponent(out NetworkObject networkObject))
                    {
                        networkObject.SpawnWithOwnership(OwnerClientId);
                    }
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


