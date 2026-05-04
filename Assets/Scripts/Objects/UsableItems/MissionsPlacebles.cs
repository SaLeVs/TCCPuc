using Inputs;
using Interfaces;
using Missions.PersonalMissions;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace Objects.UsableItems
{
    public class MissionPlaceable : NetworkBehaviour, IUsable
    {
        [SerializeField] private InputReader inputReader;

        private PlayerInteractor _interactor;
        private GameObject _playerGameObject;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            inputReader.OnUseEvent += InputReader_OnUseEvent;

            NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);

            if (playerNetworkObject == null) return;

            _playerGameObject = playerNetworkObject.gameObject;
            _playerGameObject.TryGetComponent(out _interactor);
        }

        private void InputReader_OnUseEvent()
        {
            Use(_playerGameObject);
        }

        public bool CanUse(GameObject playerInteractor)
        {
            if (playerInteractor.TryGetComponent(out PlayerState playerState) && playerState.IsDead)
                return false;

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
            
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            inputReader.OnUseEvent -= InputReader_OnUseEvent;
            _playerGameObject = null;
            _interactor = null;
        }
    }
}