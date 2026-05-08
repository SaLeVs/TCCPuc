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
        
        public bool IsOn { get; private set; }
        
        private MissionOwnershipSelector _ownershipSelector;
        private LampsManager _lampsManager;
        
        
        public void Initialize(LampsManager lampsManager, MissionOwnershipSelector ownershipSelector)
        {
            _lampsManager = lampsManager;
            _ownershipSelector = ownershipSelector;
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
            IsOn = !IsOn;
            lampLight.enabled = IsOn;

            if (playerInteractor.TryGetComponent(out NetworkObject networkObject))
            {
                OnLampToggled?.Invoke(networkObject.OwnerClientId, IsOn);
            }
        }
        
        
        public void Uninitialize()
        {
            _ownershipSelector = null;
            _lampsManager = null;
        }
        
    }
}