using Interfaces;
using Systems;
using Unity.Netcode;
using UnityEngine;
using ScriptableObjects;

namespace Missions.PersonalMissions
{
    public class MissionTotem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDataSO requiredItem;
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private Transform visualSpawnPoint;


        private NetworkVariable<bool> _isComplete = new NetworkVariable<bool>();
        public bool IsComplete => _isComplete.Value;
        

        public bool CanInteract(GameObject interactor)
        {
            if (_isComplete.Value) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;

            return ownershipSelector.IsMissionOwner(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor) => false;

        public bool TryDeposit(ulong clientId, int itemId)
        {
            if (_isComplete.Value) return false;
            if (!ownershipSelector.IsMissionOwner(clientId)) return false;
            if (itemId != requiredItem.itemId) return false;
            
            NotifyMissionCompletedRpc();
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc()
        {
            _isComplete.Value = true;
            SpawnVisualInTotem();
            Debug.Log("MissionTotem: Mission completed!");
        }

        private void SpawnVisualInTotem()
        {
            Instantiate(requiredItem.prefabVisual, visualSpawnPoint.position, visualSpawnPoint.rotation);
        }
    }
}

