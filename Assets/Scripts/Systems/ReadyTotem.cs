using Unity.Netcode;
using UnityEngine;
using Interfaces;

namespace Systems
{
    public class ReadyTotem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private PlayersReady playersReady;
        [SerializeField] private GameObject totemVisual;

        private NetworkVariable<ulong> _ownerClientId = new NetworkVariable<ulong>();
        private NetworkVariable<bool> _isEnabled = new NetworkVariable<bool>();
        private NetworkVariable<bool> _isActivated = new NetworkVariable<bool>();

        
        public override void OnNetworkSpawn()
        {
            totemVisual.SetActive(false);
            _isEnabled.OnValueChanged += ReadyTotem_IsEnabled;
            
        }

        private void ReadyTotem_IsEnabled(bool previousValue, bool newValue)
        {
            totemVisual.SetActive(newValue);
        }

        public void AssignToPlayer(ulong clientId)
        {
            _ownerClientId.Value = clientId;
        }

        public void Activate()
        {
            _isEnabled.Value = true;
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out NetworkObject netObj)) return false;

            InteractServerRpc(netObj);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out NetworkObject playerNetworkObject)) return;

            if (!CanInteract(playerNetworkObject.gameObject)) return;

            _isActivated.Value = true;
            playersReady.SetPlayerReady();
        }
        
        public bool CanInteract(GameObject interactor)
        {
            if (!_isEnabled.Value || _isActivated.Value) return false;
            if (!interactor.TryGetComponent(out NetworkObject netObj)) return false;

            return netObj.OwnerClientId == _ownerClientId.Value;
        }
        
        public override void OnNetworkDespawn()
        {
            _isEnabled.OnValueChanged -= ReadyTotem_IsEnabled;
            
        }
        
    } 
}

