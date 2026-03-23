using System;
using Components;
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
        private NetworkObject _currentSpawnedItem;
        
        
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
            if (_currentSlotSelected != slotSelected)
            {
                SelectSlot(slotSelected);
                return;
            }

            DeselectSlot();
        }
        
        private void SelectSlot(int slotSelected)
        {
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
            
            DestroyItemRpc();
            CreateItemRpc(_currentItemId);
        }

        private void DeselectSlot()
        {
            _currentSlotSelected = -1;
            _currentItemId = -1;

            OnSelectedSlotChanged?.Invoke(_currentSlotSelected);

            DestroyItemRpc();
            Debug.Log("Slot deselected");
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
                DestroyItemRpc();
                CreateItemRpc(_currentItemId);
            }
            
        }
        
        
        public void TryAddItemServer(int itemId)
        {
            if (IsServer)
            {
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
            
        }

        public void TryRemoveItemServer()
        {
            if (IsServer)
            {
                if (_currentSlotSelected <= 0) return;

                int index = _currentSlotSelected - 1;

                if (index < 0 || index >= _slots.Count) return;

                _slots[index] = -1;

                Debug.Log($"Removed item from slot {index}");

                _currentSlotSelected = -1;
                _currentItemId = -1;

                DestroyItemRpc();
                OnSelectedSlotChanged?.Invoke(_currentSlotSelected);
            }
            
        }
    
        [Rpc(SendTo.Server)]
        private void CreateItemRpc(int itemId)
        {
            if (IsServer)
            {
                if(itemId == -1) return;

                if (_currentSpawnedItem != null && _currentSpawnedItem.IsSpawned)
                {
                    _currentSpawnedItem.Despawn();
                    _currentSpawnedItem = null;
                }
                
                ItemDataSO item = itemDatabase.GetItem(itemId);
                
                if (item != null)
                {
                    GameObject itemObject = Instantiate(item.prefabUsable, playerHand.position, playerHand.rotation);

                    if (itemObject.TryGetComponent(out FollowTransform followTransform))
                    {
                        followTransform.SetTarget(playerHand);
                    }
                    
                    if(itemObject.TryGetComponent(out NetworkObject networkObject))
                    {
                        networkObject.SpawnWithOwnership(OwnerClientId);
                        _currentSpawnedItem = networkObject;
                    }
                }
            }
            
        }

        [Rpc(SendTo.Server)]
        private void DestroyItemRpc()
        {
            if (IsServer)
            {
                if (_currentSpawnedItem != null && _currentSpawnedItem.IsSpawned)
                {
                    _currentSpawnedItem.Despawn();
                    _currentSpawnedItem = null;
                }
            }
        }

        public void ReplaceSelectedItemServer(int newItemId, Vector3 itemPosition, Quaternion itemRotation)
        {
            if (IsServer)
            {
                if (_currentSlotSelected <= 0) return;
                
                int index = _currentSlotSelected - 1;
                int oldItemId = _slots[index];

                if (oldItemId == -1) return;
                
                ItemDataSO oldItem = itemDatabase.GetItem(oldItemId);

                if (oldItem != null)
                {
                    GameObject droppedItem = Instantiate(oldItem.prefabPickable, itemPosition, itemRotation);

                    if (droppedItem.TryGetComponent(out NetworkObject networkObject))
                    {
                        networkObject.Spawn();
                    }
                }
                
                _slots[index] = newItemId;
                
            }
        }

        public bool HasInventorySpace()
        {
            return _slots.Contains(-1);
        }

        public int GetItemInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
            {
                return -1;
            }
            
            return _slots[slotIndex];
        }
        
    }
}


