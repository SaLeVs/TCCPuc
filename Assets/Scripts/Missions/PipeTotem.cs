using System.Collections.Generic;
using Enums;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class PipeTotem : TotemsMissionsBase, IInteractable
    {
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        public bool IsCorrect => IsRotationCorrect();
        public int CurrentStep => _currentRotationStep.Value;

        private readonly NetworkVariable<int> _currentRotationStep = new();
        private readonly NetworkList<int> _correctSteps = new();
        
        private Quaternion _baseRotation;

        private List<float> PossibleAngles => (Manager as MissionPipesManager)?.PossiblePipesAngles;
        
        public void Initialize(MissionPipesManager manager, List<float> possibleAngles, List<int> correctSteps, int initialStepIndex)
        {
            InitializeBase(manager);
            _baseRotation = transform.localRotation;

            if (!IsServer) return;

            foreach (int step in correctSteps)
            {
                _correctSteps.Add(step);
            }

            int count = manager.PossiblePipesAngles?.Count ?? 1;
            _currentRotationStep.Value = Mathf.Clamp(initialStepIndex, 0, count - 1);
        }

        public override void OnNetworkSpawn()
        {
            _currentRotationStep.OnValueChanged += PipeTotem_OnRotationChanged;
            ApplyRotation(_currentRotationStep.Value);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;
            return CheckOwnership(networkObject.OwnerClientId);
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
            if (Manager.IsComplete) return;

            var angles = PossibleAngles;
            if (angles == null || angles.Count == 0) return;

            _currentRotationStep.Value = (_currentRotationStep.Value + 1) % angles.Count;

            ((MissionPipesManager)Manager).OnPipeRotated(clientId);
        }

        private void PipeTotem_OnRotationChanged(int previousValue, int newValue)
        {
            ApplyRotation(newValue);
        }

        private void ApplyRotation(int step)
        {
            List<float> angles = PossibleAngles;
            if (angles == null || angles.Count == 0) return;

            int safeStep = Mathf.Clamp(step, 0, angles.Count - 1);
            float angle = angles[safeStep];
            transform.localRotation = _baseRotation * Quaternion.AngleAxis(angle, rotationAxis);
        }

        private bool IsRotationCorrect()
        {
            return _correctSteps.Contains(_currentRotationStep.Value);
        }

        public void Uninitialize()
        {
            _correctSteps.Clear();
        }

        
        public override void OnNetworkDespawn()
        {
            _currentRotationStep.OnValueChanged -= PipeTotem_OnRotationChanged;
        }
        
    }
}