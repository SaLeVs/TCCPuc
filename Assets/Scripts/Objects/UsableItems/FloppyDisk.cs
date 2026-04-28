using Inputs;
using Interfaces;
using Player;
using Systems;
using Unity.Netcode;
using UnityEngine;

namespace Objects.UsableItems
{
    public class FloppyDisk : NetworkBehaviour, IUsable
    {
        [SerializeField] private InputReader inputReader;

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
            if (playerInteractor.TryGetComponent(out PlayerState playerState) && playerState.IsDead)
            {
                return false;
            }
            
            return _interactor?.CurrentInteractable is FloppyDiskTotem totem && !totem.IsComplete;
        }

        public void Use(GameObject playerInteractor)
        {
            if (!CanUse(playerInteractor)) return;

            if (_interactor?.CurrentInteractable is FloppyDiskTotem totem)
            {
                PlaceDiskServerRpc(totem.NetworkObjectId);
            }
        }

        [Rpc(SendTo.Server)]
        private void PlaceDiskServerRpc(ulong totemNetworkId)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(totemNetworkId, out NetworkObject netObj)) return;
            if (!netObj.TryGetComponent(out FloppyDiskTotem totem)) return;

            totem.PlaceDiskServer();

            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);

            if (playerNetObj.TryGetComponent(out PlayerInventory inventory))
            {
                inventory.TryRemoveItemServer();
            }
        }

        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnUseEvent -= InputReader_OnUseEvent;
                _playerInteractor = null;
                _interactor = null; 
            }
        }
        
    }
}