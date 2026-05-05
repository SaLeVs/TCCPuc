using Interfaces;
using Unity.Netcode;
using UnityEngine;
using ScriptableObjects;

namespace Missions.PersonalMissions
{
    public class MissionTotem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDataSO requiredItem;
        [SerializeField] private Transform visualSpawnPoint;
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;
        
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
            if (ownershipSelector == null) return false;
            
            bool isOwner = ownershipSelector.IsMissionOwner(clientId);
            if (!isOwner) return false;
            
            if (itemId != requiredItem.itemId) return false;

            _isComplete.Value = true;
            missionCompleter.Complete();
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc()
        {
            SpawnVisualInTotem();
            Debug.Log("MissionTotem: Mission completed!");
        }
        
        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyOwnerMissionCompletedRpc(RpcParams rpcParams = default)
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompletePersonalMission(ownershipSelector.Mission);
            }
        }

        private void SpawnVisualInTotem()
        {
            Instantiate(requiredItem.prefabVisual, visualSpawnPoint.position, visualSpawnPoint.rotation);
        }
    }
}

