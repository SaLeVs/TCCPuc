using System;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class LampTotem : NetworkBehaviour, IInteractable
    {
        public event Action<ulong, bool> OnLampToggled;
        
        [SerializeField] private Light lampLight;
        
        private NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public bool IsOn => _isOn.Value;
        
        private MissionOwnershipSelector _ownershipSelector;
        private LampsManager _lampsManager;
        
        
        public override void OnNetworkSpawn()
        {
            _isOn.OnValueChanged += HandleLampStateChanged;
            lampLight.enabled = _isOn.Value; 
        }

        public void Initialize(LampsManager lampsManager, MissionOwnershipSelector ownershipSelector, bool initialState)
        {
            _lampsManager = lampsManager;
            _ownershipSelector = ownershipSelector;
            _isOn.Value = initialState;
        }

        public bool CanInteract(GameObject interactor)
        {
            if (interactor.TryGetComponent(out NetworkObject networkObject))
            {
                return _ownershipSelector.IsMissionOwner(networkObject.OwnerClientId);
            }

            return false;
        }

        public bool Interact(GameObject playerInteractor)
        {
            ToggleLamp(playerInteractor);
            return true;
        }

        private void ToggleLamp(GameObject playerInteractor)
        {
            if (!IsServer) return;

            _isOn.Value = !_isOn.Value;

            if (playerInteractor.TryGetComponent(out NetworkObject networkObject))
            {
                OnLampToggled?.Invoke(networkObject.OwnerClientId, _isOn.Value);
            }
        }

        private void HandleLampStateChanged(bool previousValue, bool newValue)
        {
            lampLight.enabled = newValue;
        }

        public override void OnNetworkDespawn()
        {
            _isOn.OnValueChanged -= HandleLampStateChanged;
        }

        
        public void Uninitialize()
        {
            _ownershipSelector = null;
            _lampsManager = null;
        }
        
    }
}