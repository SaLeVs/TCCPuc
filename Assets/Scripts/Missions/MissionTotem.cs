using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;
using ScriptableObjects;

namespace Missions.PersonalMissions
{
    public class MissionTotem : NetworkBehaviour, IInteractable
    {
        public event Action<ulong> OnTotemDeposited;
        
        [SerializeField] private ItemDataSO expectedItem;
        [SerializeField] private Transform spawnPoint;
        
        
        public bool IsSlotCorrect => _currentItemId == expectedItem.itemId;
        
        private MissionOwnershipSelector _ownershipSelector;
        private MissionTotemGroup _totemGroup;
        
        private NetworkObject _currentPickable;
        private int _currentItemId = -1;
        

        public void Initialize(MissionTotemGroup totemGroup, MissionOwnershipSelector ownershipSelector)
        {
            _totemGroup = totemGroup;
            _ownershipSelector = ownershipSelector;
        }
        
        
        public bool CanInteract(GameObject interactor)
        {
            if (_totemGroup.IsComplete) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;

            Debug.Log($"Is mission Owner: {_ownershipSelector.IsMissionOwner(networkObject.OwnerClientId)}");
            return _ownershipSelector.IsMissionOwner(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor) => false;

        public bool TryDeposit(ulong clientId, int itemId)
        {
            if (_totemGroup.IsComplete)
            {
                Debug.Log($"Is totemCompleted: {_totemGroup.IsComplete}");
                return false;
            }
            
            if (_ownershipSelector == null)
            {
                Debug.Log($"Is ownership selector null: {_ownershipSelector}");
                return false;
            }
            
            if (!_ownershipSelector.IsMissionOwner(clientId))
            {
                Debug.Log($"Is mission owner: {_ownershipSelector.IsMissionOwner(clientId)}");
                return false;
            }
            if (itemId != expectedItem.itemId)
            {
                Debug.Log($"Is item ID equal expected item Id: {itemId != expectedItem.itemId}");
                return false;
            }

            if (_currentPickable != null)
            {
                _currentPickable.Despawn();
            }

            _currentItemId = itemId;

            GameObject spawned = Instantiate(expectedItem.prefabPickable, spawnPoint.position, spawnPoint.rotation);

            if (spawned.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
                _currentPickable = netObj;
            }

            Debug.Log("On totem deposited");
            OnTotemDeposited?.Invoke(clientId);
            return true;
        }

        public void Uninitialize()
        {
            _ownershipSelector = null;
            _totemGroup = null;
        }
        
    }
}