using System;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class LampTotem : TotemsMissionsBase, IInteractable
    {
        public event Action<ulong, bool> OnLampToggled;
        
        [SerializeField] private Light lampLight;
        
        
        public bool IsOn => _isOn.Value;
        
        private readonly NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public override void OnNetworkSpawn()
        {
            _isOn.OnValueChanged += LampTotem_OnLampStateChanged;
            lampLight.enabled = _isOn.Value; 
        }

        public void Initialize(LampsManager lampManager, bool initialState)
        {
            InitializeBase(lampManager);
            
            if (!IsServer) return;
            
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
                OnLampToggled?.Invoke(networkObject.OwnerClientId, _isOn.Value);
            }
        }

        private void LampTotem_OnLampStateChanged(bool previousValue, bool newValue) => lampLight.enabled = newValue;

        public override void OnNetworkDespawn() => _isOn.OnValueChanged -= LampTotem_OnLampStateChanged;
        
    }
}