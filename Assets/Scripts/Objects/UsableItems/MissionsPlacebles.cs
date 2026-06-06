using Inputs;
using Interfaces;
using Missions.PersonalMissions;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Objects.UsableItems
{
    public class MissionPlaceable : NetworkBehaviour, IUsable
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private ItemDataSO itemData;
        
        private GameObject _playerInteractor;
        private PlayerInteractor _interactor;
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnUseEvent += InputReader_OnUseEvent;

                NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);

                if (playerNetworkObject != null)
                {
                    _playerInteractor = playerNetworkObject.gameObject;
                    if (_playerInteractor.TryGetComponent(out _interactor)) ;
                }
            }
        }

        private void InputReader_OnUseEvent()
        {
            Use(_playerInteractor);
        } 

        public bool CanUse(GameObject playerInteractor)
        {
            if (playerInteractor.TryGetComponent(out PlayerState playerState) && playerState.IsDead) return false;

            return _interactor?.CurrentInteractable is MissionTotem;
        }

        public void Use(GameObject playerInteractor)
        {
            if (!CanUse(playerInteractor)) return;

            if (_interactor.CurrentInteractable is MissionTotem totem)
            {
                TryPlaceServerRpc(totem.NetworkObjectId);
            }
        }

        [Rpc(SendTo.Server)]
        private void TryPlaceServerRpc(ulong totemNetworkId)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(totemNetworkId, out NetworkObject netObj)) return;
            if (!netObj.TryGetComponent(out MissionTotem totem)) return;
            if (!totem.TryDeposit(OwnerClientId, itemData.itemId)) return;

            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);
            if (playerNetObj == null) return;

            if (playerNetObj.TryGetComponent(out PlayerInventory inventory))
            {
                inventory.TryRemoveItemServer(itemData.itemId);
            }
            
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            inputReader.OnUseEvent -= InputReader_OnUseEvent;
            _playerInteractor = null;
            _interactor = null;
        }
    }
}