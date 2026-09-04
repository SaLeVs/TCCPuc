using System;
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
    public class Door : NetworkBehaviour, IInteractable, IForceableDoor
    {
        public static Action<Vector3> OnDoorBlockedSound;

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

        [Tooltip("How long the door refuses to be touched after the monster forces it, so nobody can shut it back in its face on a loop.")]
        [SerializeField] private float monsterLockSeconds = 3f;

        public Vector3 Position => transform.position;
        public bool IsClosed => _state.Value == DoorState.Closed;
        public bool IsSwinging => _rotateRoutine != null;

        /// <summary>Server-side: the monster is holding this door and it will not answer players yet.</summary>
        public bool IsLocked => Time.time < _lockedUntilTime;

        private readonly NetworkVariable<DoorState> _state = new NetworkVariable<DoorState>(DoorState.Closed);

        private Coroutine _rotateRoutine;

        // Source of truth for the swing. Reading the start angle back off a Transform is what broke
        // closing: the server only ever drove the Rigidbody, so the Transform stayed at rest.
        private float _currentAngle;

        // +1 / -1 while swinging, so an impact knows which way the leaf is travelling.
        private float _swingSign;

        private float _lockedUntilTime;

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += OnStateChanged;

            // The doorway always stays on the navmesh. Carving it away is what used to make a
            // closed door unreachable — the monster now walks up to it and forces it open instead.
            navMeshObstacle.enabled = false;

            ApplyAngle(AngleFor(_state.Value), snap: true);

            if (!IsServer) return;

            // The root has no Collider — the leaf does — so hits arrive through the relay.
            if (leafCollisionRelay != null)
            {
                leafCollisionRelay.OnLeafCollisionEnter += LeafCollisionRelay_OnLeafCollisionEnter;
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateChanged;

            if (IsServer)
            {
                if (leafCollisionRelay != null)
                {
                    leafCollisionRelay.OnLeafCollisionEnter -= LeafCollisionRelay_OnLeafCollisionEnter;
                }
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
            // The monster is holding this door — either it just forced it and shutting it straight
            // back would be a door-slamming contest, or it sabotaged the whole floor. Rattle it so
            // the refusal reads as the door being held, not as the input being dropped.
            if (IsLocked)
            {
                PlayBlockedSoundRpc();
                return;
            }

            if (_state.Value != DoorState.Closed)
            {
                _state.Value = DoorState.Closed;
                return;
            }

            OpenAwayFrom(playerPosition);
        }

        /// <summary>
        /// Server-side open with no interactor behind it — what the monster uses after shouldering
        /// the door. Never closes: forcing a door only ever opens it.
        /// </summary>
        public void ForceOpenFrom(Vector3 fromPosition)
        {
            if (!IsServer) return;

            // Hold the door shut to interaction whether or not it was already open: the monster is
            // right there either way.
            _lockedUntilTime = Time.time + monsterLockSeconds;

            OpenAwayFrom(fromPosition);
        }

        /// <summary>
        /// The monster's door sabotage: slam it shut and hold it. Nothing calls Restore on a
        /// timer, so the hold has to expire by itself — players get the door back when it does.
        /// </summary>
        public void CloseAndLock(float seconds)
        {
            if (!IsServer) return;

            _lockedUntilTime = Time.time + seconds;

            if (_state.Value != DoorState.Closed)
            {
                _state.Value = DoorState.Closed;
            }
        }

        public void ClearLock()
        {
            if (!IsServer) return;

            _lockedUntilTime = 0f;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayBlockedSoundRpc()
        {
            // Positional, so anyone nearby hears someone failing to get through.
            OnDoorBlockedSound?.Invoke(transform.position);
        }

        private void OpenAwayFrom(Vector3 fromPosition)
        {
            if (!IsServer || _state.Value != DoorState.Closed) return;

            Vector3 toOpener = fromPosition - doorPivot.position;
            toOpener.y = 0f;

            // Swing away from whoever opened it.
            float side = Vector3.Dot(doorPivot.forward, toOpener.normalized);

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
