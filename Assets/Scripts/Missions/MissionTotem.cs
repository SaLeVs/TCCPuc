using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;
using ScriptableObjects;

namespace Missions.PersonalMissions
{
    public class MissionTotem : TotemsMissionsBase, IInteractable
    {
        public event Action<ulong> OnTotemDeposited;
        
        [SerializeField] private ItemDataSO expectedItem;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private ItemListSO itemDatabase;
        
        
        public bool IsSlotCorrect => _currentItemId == expectedItem.itemId;
        
        private NetworkObject _currentPickable;
        private int _currentItemId = -1;
        

        public void Initialize(MissionTotemGroup totemGroup)
        {
            InitializeBase(totemGroup);
        }
        
        public bool CanInteract(GameObject interactor)
        {
            if (HasItemInSlot()) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;

            return CheckOwnership(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor) => false;

        public bool TryDeposit(ulong clientId, int itemId)
        {
            if (!CheckOwnership(clientId)) return false;
            if (HasItemInSlot()) return false;

            _currentItemId = itemId;

            ItemDataSO item = itemDatabase.GetItem(itemId);

            if (item == null) return false;

            GameObject spawned = Instantiate(item.prefabPickable, spawnPoint.position, spawnPoint.rotation);

            if (spawned.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
                _currentPickable = netObj;

                if (spawned.TryGetComponent(out IMissionOwnerAware ownerAware))
                {
                    ownerAware.SetOwnershipSelector(Manager);
                }
            }

            OnTotemDeposited?.Invoke(clientId);
            return true;
        }
        
        private bool HasItemInSlot()
        {
            if (_currentPickable == null) return false;

            if (!_currentPickable || !_currentPickable.IsSpawned)
            {
                ClearSlot();
                return false;
            }

            return true;
        }
        
        private void ClearSlot()
        {
            _currentPickable = null;
            _currentItemId = -1;
        }
        
    }
}