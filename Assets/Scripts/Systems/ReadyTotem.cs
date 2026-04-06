using Unity.Netcode;
using UnityEngine;
using Interfaces;

namespace Systems
{
    public class ReadyTotem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private PlayersReady playersReady;
        [SerializeField] private GameObject totemVisual;

        private NetworkVariable<ulong> _ownerClientId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        private NetworkVariable<bool> _isEnabled = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        private NetworkVariable<bool> _isActivated = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        
        public override void OnNetworkSpawn()
        {
            _isEnabled.OnValueChanged += ReadyTotem_IsEnabled;
            _isActivated.OnValueChanged += ReadyTotem_IsActivated;
            
            totemVisual.SetActive(_isEnabled.Value);
            
        }
        
        
        private void ReadyTotem_IsEnabled(bool previousValue, bool newValue)
        {
            totemVisual.SetActive(newValue);
        }
        
        private void ReadyTotem_IsActivated(bool previousValue, bool newValue)
        {
            if (newValue)
            {
                totemVisual.SetActive(false); 
            }
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
            playersReady.SetPlayerReadyServerRpc(playerNetworkObject.OwnerClientId);
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
            _isActivated.OnValueChanged -= ReadyTotem_IsActivated;
            
        }
        
    } 
}

