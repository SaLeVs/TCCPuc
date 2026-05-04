using Interfaces;
using Player;
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
        
        
        private NetworkVariable<bool> _isComplete = new NetworkVariable<bool>(false);
        public bool IsComplete => _isComplete.Value;
        

        public bool CanInteract(GameObject interactor)
        {
            
            if (_isComplete.Value) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;
            
            return ownershipSelector.IsMissionOwner(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;

            NetworkObject networkObject = playerInteractor.GetComponent<NetworkObject>();

            if (!IsServer)
            {
                InteractServerRpc(networkObject);
                return true;
            }

            TryDepositItem(playerInteractor);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef)
        {
            if (playerRef.TryGet(out NetworkObject playerNetworkObject))
                TryDepositItem(playerNetworkObject.gameObject);
        }

        private void TryDepositItem(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out PlayerInventory inventory)) return;
            

            inventory.TryRemoveItemServer();
            _isComplete.Value = true;

            NotifyMissionCompletedRpc();
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyWrongItemRpc(RpcParams rpcParams = default)
        {
            // Add feedbacks here
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc()
        {
            // Add feedbacks here
        }
    }
}

