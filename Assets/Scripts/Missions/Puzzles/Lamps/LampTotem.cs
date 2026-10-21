using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Puzzles
{
    public class LampTotem : PuzzlePieceBase, IInteractable
    {
        [SerializeField] private Light lampLight;

        [Tooltip("Ceiling fixture turned to face the aim point. Its lens looks down the mesh's local -X.")]
        [SerializeField] private Transform fixture;

        private static readonly Quaternion LensFacing = Quaternion.Euler(0f, 90f, 0f);


        public bool IsOn => _isOn.Value;
        public override bool IsCorrect => _isOn.Value == _requiredState;

        private readonly NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Zero means "not aimed": the light keeps the angle it has in the prefab.
        private readonly NetworkVariable<Vector3> _aimPoint = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private bool _requiredState;

        public override void OnNetworkSpawn()
        {
            _isOn.OnValueChanged += LampTotem_OnLampStateChanged;
            _aimPoint.OnValueChanged += LampTotem_OnAimPointChanged;
            lampLight.enabled = _isOn.Value;
            AimAt(_aimPoint.Value);
        }

        public void Setup(bool requiredState, bool initialState)
        {
            if (!IsServer) return;

            _requiredState = requiredState;
            _isOn.Value = initialState;
        }

        /// <summary>Server-side: every peer turns its own copy of the light towards this point.</summary>
        public void SetAimPoint(Vector3 worldPoint)
        {
            if (!IsServer) return;

            _aimPoint.Value = worldPoint;
        }

        private void AimAt(Vector3 worldPoint)
        {
            if (worldPoint == Vector3.zero) return;

            if (fixture != null)
            {
                Vector3 flat = worldPoint - fixture.position;
                flat.y = 0f;

                if (flat.sqrMagnitude > 0.0001f)
                    fixture.rotation = Quaternion.LookRotation(flat) * LensFacing;
            }

            lampLight.transform.rotation = Quaternion.LookRotation(worldPoint - lampLight.transform.position);
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

        private void LampTotem_OnAimPointChanged(Vector3 previousValue, Vector3 newValue) => AimAt(newValue);

        public override void OnNetworkDespawn()
        {
            _isOn.OnValueChanged -= LampTotem_OnLampStateChanged;
            _aimPoint.OnValueChanged -= LampTotem_OnAimPointChanged;
        }

    }
}
