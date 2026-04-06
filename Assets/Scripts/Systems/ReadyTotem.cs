using Unity.Netcode;
using UnityEngine;
using Interfaces;

namespace Systems
{
    public class ReadyTotem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private PlayersReady playersReady;
        [SerializeField] private GameObject totemVisual;

        private ulong _ownerClientId;
        private bool _isActivated;
        private bool _isEnabled;

        
        public override void OnNetworkSpawn()
        {
            totemVisual.SetActive(false);
            
        }
        
        public void AssignToPlayer(ulong clientId)
        {
            _ownerClientId = clientId;
        }

        public void Activate()
        {
            _isEnabled = true;
            totemVisual.SetActive(_isEnabled);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!_isEnabled || _isActivated) return false;
            if (!interactor.TryGetComponent(out NetworkObject netObj)) return false;
            
            return netObj.OwnerClientId == _ownerClientId;
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;

            _isActivated = true;
            playersReady.SetPlayerReady();
            Debug.Log($"Player {playerInteractor.name} activated their totem!");
            return true;
        }
        
    } 
}

