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
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionTotemGroup totemGroup;

        private NetworkObject _currentPickable;
        private int _currentItemId = -1;

        public bool IsSlotCorrect => _currentItemId == expectedItem.itemId;
        public bool IsOccupied => _currentItemId != -1;

        public bool CanInteract(GameObject interactor)
        {
            if (totemGroup.IsComplete) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;

            return ownershipSelector.IsMissionOwner(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor) => false;

        public bool TryDeposit(ulong clientId, int itemId)
        {
            if (totemGroup.IsComplete) return false;
            if (ownershipSelector == null) return false;
            if (!ownershipSelector.IsMissionOwner(clientId)) return false;

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

            OnTotemDeposited?.Invoke(clientId);
            return true;
        }
    }
}