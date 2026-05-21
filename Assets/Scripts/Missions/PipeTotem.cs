using System.Collections.Generic;
using Enums;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class PipeTotem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private PipeType pipeType;

        public bool IsCorrect => IsRotationCorrect();

        private NetworkVariable<int> _currentRotationStep = new NetworkVariable<int>();

        private MissionPipesManager _pipesManager;
        private MissionOwnershipSelector _ownershipSelector;

        private List<Vector3> _rotations = new();
        private List<Vector3> _correctRotations = new();
        

        public void Initialize(MissionPipesManager manager, MissionOwnershipSelector selector, List<Vector3> rotations, 
            List<Vector3> correctRotations, int initialStepIndex)
        {
            _pipesManager = manager;
            _ownershipSelector = selector;
            _rotations = rotations;
            _correctRotations = correctRotations;

            if (_rotations.Count == 0)
            {
                Debug.LogError("PipeTotem: Rotation list empty");
                return;
            }

            if (!IsServer) return;

            _currentRotationStep.Value =
                Mathf.Clamp(initialStepIndex, 0, _rotations.Count - 1);

            ApplyRotation(_currentRotationStep.Value);
        }

        public override void OnNetworkSpawn()
        {
            _currentRotationStep.OnValueChanged += PipeTotem_OnRotationChanged;
            ApplyRotation(_currentRotationStep.Value);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (_pipesManager != null && _pipesManager.IsComplete) return false;
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
            if (!IsServer) return;
            if (_rotations == null || _rotations.Count == 0) return;
            if (_pipesManager == null || _pipesManager.IsComplete) return;

            _currentRotationStep.Value = (_currentRotationStep.Value + 1) % _rotations.Count;
            _pipesManager.OnPipeRotated(clientId);
        }

        private void PipeTotem_OnRotationChanged(int previousValue, int newValue)
        {
            ApplyRotation(newValue);
        }

        private void ApplyRotation(int step)
        {
            if (_rotations == null || _rotations.Count == 0) return;

            int safeStep = Mathf.Clamp(step, 0, _rotations.Count - 1);
            transform.localRotation = Quaternion.Euler(_rotations[safeStep]);
        }
        
        public bool IsRotationCorrect()
        {
            if (_rotations == null || _rotations.Count == 0)
                return false;
            Vector3 currentRotation = _rotations[_currentRotationStep.Value];

            foreach (Vector3 correctRotation in _correctRotations)
            {
                if (currentRotation == correctRotation)
                {
                    return true;
                }
            }

            return false;
        }
        
        public void Uninitialize()
        {
            _pipesManager = null;
            _ownershipSelector = null;
            _rotations.Clear();
        }

        
        public override void OnNetworkDespawn()
        {
            _currentRotationStep.OnValueChanged -= PipeTotem_OnRotationChanged;
        }
        
    }
}