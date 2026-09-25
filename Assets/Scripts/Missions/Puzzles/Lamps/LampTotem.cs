using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Puzzles
{
    public class LampTotem : PuzzlePieceBase, IInteractable
    {
        [SerializeField] private Light lampLight;


        public bool IsOn => _isOn.Value;
        public override bool IsCorrect => _isOn.Value == _requiredState;

        private readonly NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private bool _requiredState;

        public override void OnNetworkSpawn()
        {
            _isOn.OnValueChanged += LampTotem_OnLampStateChanged;
            lampLight.enabled = _isOn.Value;
        }

        public void Setup(bool requiredState, bool initialState)
        {
            if (!IsServer) return;

            _requiredState = requiredState;
            _isOn.Value = initialState;
        }

        public bool CanInteract(GameObject interactor)
        {
            if(interactor.TryGetComponent(out NetworkObject networkObject))
            {
                return CheckOwnership(networkObject.OwnerClientId);
            }

            return false;
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;

            if (!IsServer)
            {
                if (playerInteractor.TryGetComponent(out NetworkObject networkObject))
                {
                    ToggleLampServerRpc(networkObject);
                }
                return true;
            }

            ToggleLamp(playerInteractor);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void ToggleLampServerRpc(NetworkObjectReference playerRef)
        {
            if (playerRef.TryGet(out NetworkObject playerNetObj))
            {
                ToggleLamp(playerNetObj.gameObject);
            }
        }

        private void ToggleLamp(GameObject playerInteractor)
        {
            if (!IsServer || Manager.IsComplete) return;

            _isOn.Value = !_isOn.Value;

            if (playerInteractor.TryGetComponent(out NetworkObject networkObject))
            {
                NotifyChanged(networkObject.OwnerClientId);
            }
        }

        private void LampTotem_OnLampStateChanged(bool previousValue, bool newValue) => lampLight.enabled = newValue;

        public override void OnNetworkDespawn() => _isOn.OnValueChanged -= LampTotem_OnLampStateChanged;

    }
}
