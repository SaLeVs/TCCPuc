using System.Collections.Generic;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Puzzles
{
    public class PipeTotem : PuzzlePieceBase, IInteractable
    {
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        public override bool IsCorrect => IsRotationCorrect();
        public int CurrentStep => _currentRotationStep.Value;

        private readonly NetworkVariable<int> _currentRotationStep = new();
        private readonly NetworkList<int> _correctSteps = new();

        // Só o servidor conhece (Setup) e usa: os clientes recebem a rotação pelo NetworkTransform.
        private Quaternion _baseRotation;
        private bool _hasBaseRotation;

        private List<float> PossibleAngles => (Manager as PipesPuzzleManager)?.PossiblePipesAngles;

        public void Setup(List<int> correctSteps, int initialStepIndex)
        {
            if (!IsServer) return;

            _baseRotation = transform.localRotation;
            _hasBaseRotation = true;

            foreach (int step in correctSteps)
            {
                _correctSteps.Add(step);
            }

            int count = PossibleAngles?.Count ?? 1;
            _currentRotationStep.Value = Mathf.Clamp(initialStepIndex, 0, count - 1);

            // Se o passo inicial for igual ao valor padrão (0), o OnValueChanged não dispara.
            ApplyRotation(_currentRotationStep.Value);
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

            NotifyChanged(clientId);
        }

        private void PipeTotem_OnRotationChanged(int previousValue, int newValue)
        {
            ApplyRotation(newValue);
        }

        // Só o servidor gira o cano; o NetworkTransform (o dono é o servidor) leva a rotação aos clientes.
        // Antes os clientes também giravam, com a rotação base zerada (o Setup só roda no servidor),
        // e o cano podia piscar errado até o NetworkTransform corrigir.
        private void ApplyRotation(int step)
        {
            if (!IsServer || !_hasBaseRotation) return;

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
