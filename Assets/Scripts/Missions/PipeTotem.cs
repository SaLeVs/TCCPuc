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
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        public bool IsCorrect => IsRotationCorrect();
        public int CurrentStep => _currentRotationStep.Value;

        private readonly NetworkVariable<int> _currentRotationStep = new();

        private MissionPipesManager _pipesManager;
        private MissionOwnershipSelector _ownershipSelector;

        private List<float> _possibleAngles = new();
        private HashSet<int> _correctSteps = new();

        private Quaternion _baseRotation;

        public void Initialize(
            MissionPipesManager manager,
            MissionOwnershipSelector selector,
            List<float> possibleAngles,
            List<int> correctSteps,
            int initialStepIndex)
        {
            _pipesManager = manager;
            _ownershipSelector = selector;

            _possibleAngles = possibleAngles;
            _correctSteps = new HashSet<int>(correctSteps);

            _baseRotation = transform.localRotation;

            _currentRotationStep.Value =
                Mathf.Clamp(initialStepIndex, 0, _possibleAngles.Count - 1);
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
            if (!IsServer) return;
            if (_pipesManager.IsComplete) return;

            _currentRotationStep.Value =
                (_currentRotationStep.Value + 1) % _possibleAngles.Count;

            _pipesManager.OnPipeRotated(clientId);
        }

        private void PipeTotem_OnRotationChanged(int previousValue, int newValue)
        {
            ApplyRotation(newValue);
        }

        private void ApplyRotation(int step)
        {
            if (_possibleAngles.Count == 0) return;

            int safeStep = Mathf.Clamp(step, 0, _possibleAngles.Count - 1);

            float angle = _possibleAngles[safeStep];

            Quaternion rotationOffset = Quaternion.AngleAxis(angle, rotationAxis);
            transform.localRotation = _baseRotation * rotationOffset;
        }

        private bool IsRotationCorrect()
        {
            return _correctSteps.Contains(_currentRotationStep.Value);
        }

        public void Uninitialize()
        {
            _pipesManager = null;
            _ownershipSelector = null;

            _possibleAngles.Clear();
            _correctSteps.Clear();
        }

        
        public override void OnNetworkDespawn()
        {
            _currentRotationStep.OnValueChanged -= PipeTotem_OnRotationChanged;
        }
        
    }
}