using System.Collections;
using Enums;
using Interfaces;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Objects
{
    [RequireComponent(typeof(NavMeshObstacle))]
    public class Door : NetworkBehaviour, IInteractable
    {
        [Header("References")]
        [SerializeField] private Transform doorPivot;
        [SerializeField] private Rigidbody doorRigidbody;
        [SerializeField] private NavMeshObstacle navMeshObstacle;
        [SerializeField] private DoorLeafCollisionRelay leafCollisionRelay;

        [Header("Impact")]
        [Tooltip("What this door does to a player it hits while swinging. Leave empty for a door that never knocks anyone over.")]
        [SerializeField] private ImpactProfileSO impactProfile;

        [Header("Angles")]
        [SerializeField] private float closedAngle;
        [FormerlySerializedAs("openAngle")]
        [SerializeField] private float openSideAAngle = 100f;
        [SerializeField] private float openSideBAngle = -100f;

        [Header("Settings")]
        [SerializeField] private float openDegreesPerSecond = 300f;

        private readonly NetworkVariable<DoorState> _state = new NetworkVariable<DoorState>(DoorState.Closed);

        private Coroutine _rotateRoutine;

        // Source of truth for the swing. Reading the start angle back off a Transform is what broke
        // closing: the server only ever drove the Rigidbody, so the Transform stayed at rest.
        private float _currentAngle;

        // +1 / -1 while swinging, so an impact knows which way the leaf is travelling.
        private float _swingSign;

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += OnStateChanged;

            navMeshObstacle.carving = _state.Value == DoorState.Closed;
            ApplyAngle(AngleFor(_state.Value), snap: true);

            // The root has no Collider — the leaf does — so hits arrive through the relay.
            if (IsServer && leafCollisionRelay != null)
            {
                leafCollisionRelay.OnLeafCollisionEnter += LeafCollisionRelay_OnLeafCollisionEnter;
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateChanged;

            if (IsServer && leafCollisionRelay != null)
            {
                leafCollisionRelay.OnLeafCollisionEnter -= LeafCollisionRelay_OnLeafCollisionEnter;
            }

            if (_rotateRoutine != null)
            {
                StopCoroutine(_rotateRoutine);
                _rotateRoutine = null;
            }
        }

        public bool CanInteract(GameObject interactor) => true;

        public bool Interact(GameObject playerInteractor)
        {
            Debug.Log($"Door interacted by {playerInteractor.name} at position {playerInteractor.transform.position}");
            RequestToggleServerRpc(playerInteractor.transform.position);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void RequestToggleServerRpc(Vector3 playerPosition)
        {
            if (_state.Value != DoorState.Closed)
            {
                _state.Value = DoorState.Closed;
                return;
            }

            Vector3 toPlayer = playerPosition - doorPivot.position;
            toPlayer.y = 0f;

            // Swing away from whoever opened it.
            float side = Vector3.Dot(doorPivot.forward, toPlayer.normalized);

            _state.Value = side > 0f ? DoorState.OpenSideB : DoorState.OpenSideA;
        }

        private float AngleFor(DoorState state)
        {
            switch (state)
            {
                case DoorState.OpenSideA: return openSideAAngle;
                case DoorState.OpenSideB: return openSideBAngle;
                default: return closedAngle;
            }
        }

        private void OnStateChanged(DoorState previous, DoorState current)
        {
            navMeshObstacle.carving = current == DoorState.Closed;

            if (_rotateRoutine != null)
            {
                StopCoroutine(_rotateRoutine);
            }

            _rotateRoutine = StartCoroutine(RotateDoor(AngleFor(current)));
        }

        private IEnumerator RotateDoor(float targetAngle)
        {
            float startAngle = _currentAngle;
            float distance = Mathf.Abs(targetAngle - startAngle);

            _swingSign = Mathf.Sign(targetAngle - startAngle);

            if (distance > 0f)
            {
                // Time scales with how far this particular swing travels, so every angle moves at
                // the configured speed instead of every swing taking the same fixed duration.
                float duration = distance / Mathf.Max(1f, openDegreesPerSecond);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    // The server moves a kinematic Rigidbody, so it advances on the physics step.
                    elapsed += IsServer ? Time.fixedDeltaTime : Time.deltaTime;

                    ApplyAngle(Mathf.Lerp(startAngle, targetAngle, elapsed / duration));

                    if (IsServer)
                    {
                        yield return new WaitForFixedUpdate();
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }

            ApplyAngle(targetAngle);

            _swingSign = 0f;
            _rotateRoutine = null;
        }

        private void ApplyAngle(float angle, bool snap = false)
        {
            _currentAngle = angle;

            Quaternion localRotation = Quaternion.Euler(0f, angle, 0f);

            if (IsServer && !snap)
            {
                doorRigidbody.MoveRotation(transform.rotation * localRotation);
            }
            else
            {
                doorRigidbody.transform.localRotation = localRotation;
            }
        }

        private void LeafCollisionRelay_OnLeafCollisionEnter(Collision collision)
        {
            if (!IsServer || impactProfile == null) return;

            // A door standing still is just scenery to walk into.
            if (_rotateRoutine == null || _swingSign == 0f) return;

            if (!collision.gameObject.TryGetComponent(out PlayerKnockdown knockdown)) return;

            Vector3 hinge = doorRigidbody.transform.position;
            Vector3 radial = collision.GetContact(0).point - hinge;
            radial.y = 0f;

            if (radial.sqrMagnitude < 0.0001f) return;

            // Linear speed of the leaf at the contact point: angular speed times the lever arm.
            float leafSpeed = openDegreesPerSecond * Mathf.Deg2Rad * radial.magnitude;
            if (leafSpeed < impactProfile.minimumSpeed) return;

            // The leaf sweeps perpendicular to the lever arm, so that is where it throws the player.
            Vector3 direction = Vector3.Cross(Vector3.up * _swingSign, radial).normalized;

            knockdown.ApplyImpact(impactProfile, direction);
        }
    }
}
