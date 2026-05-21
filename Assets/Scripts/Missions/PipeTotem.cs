using Enums;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionPipe : NetworkBehaviour, IInteractable
    {
        [SerializeField] private PipeType pipeType;
        [SerializeField] private float rotationPerStep = 90f;
        
        public bool IsCorrect => CheckCorrectRotation();
        
        private NetworkVariable<int> _currentRotationStep = new NetworkVariable<int>(0);
        
        private MissionPipesManager _pipesManager;
        private MissionOwnershipSelector _ownershipSelector;
        
        private const int STEPS_PER_ROTATION = 4;
        
        
        public void Initialize(MissionPipesManager manager, MissionOwnershipSelector selector, int randomStartStep)
        {
            _pipesManager = manager;
            _ownershipSelector = selector;
            _currentRotationStep.Value = randomStartStep;
            ApplyRotation(randomStartStep);
        }
        
        
        public override void OnNetworkSpawn()
        {
            _currentRotationStep.OnValueChanged += PipeTotem_OnRotationChanged;
            ApplyRotation(_currentRotationStep.Value);
        }
        
        
        public bool CanInteract(GameObject interactor)
        {
            if (_pipesManager.IsComplete) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;

            return _ownershipSelector.IsMissionOwner(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;
            if (!playerInteractor.TryGetComponent(out NetworkObject networkObject)) return false;

            if (!IsServer)
            {
                InteractServerRpc(networkObject);
                return true;
            }

            RotatePipe(networkObject.OwnerClientId);
            return true;
        }
        
        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef)
        {
            if (playerRef.TryGet(out NetworkObject playerNetObj))
            {
                RotatePipe(playerNetObj.OwnerClientId);
            }
        }

        private void RotatePipe(ulong clientId)
        {
            _currentRotationStep.Value = (_currentRotationStep.Value + 1) % STEPS_PER_ROTATION;
            _pipesManager.OnPipeRotated(clientId);
        }

        private void PipeTotem_OnRotationChanged(int previousValue, int newValue)
        {
            ApplyRotation(newValue);
        }

        private void ApplyRotation(int step)
        {
            transform.localRotation = Quaternion.Euler(step * rotationPerStep, 0f, 0f);
        }

        private bool CheckCorrectRotation()
        {
            switch (pipeType)
            {
                case PipeType.Straight:
                    return _currentRotationStep.Value == 0 || _currentRotationStep.Value == 2;
                case PipeType.Joint:
                    return _currentRotationStep.Value == 0;
                default:
                    return false;
            }
        }

        public void Uninitialize()
        {
            _pipesManager = null;
            _ownershipSelector = null;
        }

        public override void OnNetworkDespawn()
        {
            _currentRotationStep.OnValueChanged -= PipeTotem_OnRotationChanged;
        }
        
    }
}