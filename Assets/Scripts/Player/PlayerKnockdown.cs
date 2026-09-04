using System.Collections;
using System.Collections.Generic;
using Components;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public struct KnockdownState : INetworkSerializable
    {
        public bool Active;
        public Vector3 Impulse;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Active);
            serializer.SerializeValue(ref Impulse);
        }
    }

    /// <summary>
    /// Temporary knockdown: the player drops into a ragdoll, then stands back up somewhere the
    /// standing capsule actually fits. Death is handled by <see cref="PlayerDead"/> and wins over
    /// this — a player who dies while down stays down.
    /// </summary>
    public class PlayerKnockdown : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerState playerState;
        [SerializeField] private PlayerDead playerDead;
        [SerializeField] private PlayerCameraOffset playerCameraOffset;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private CapsuleCollider playerCapsule;
        [SerializeField] private Transform ragdollSpawnRoot;
        [SerializeField] private GameObject ragdollPrefab;

        [Header("Get Up")]
        [Tooltip("Layers that block a standing spot. Should cover the floor and anything solid: Ground, Scenary, Walls.")]
        [SerializeField] private LayerMask standingBlockingMask;
        [SerializeField] private float searchMaxDistance = 4f;
        [SerializeField] private float searchStepSize = 0.5f;
        [SerializeField] private float searchAngleStep = 20f;
        [SerializeField] private float groundOffset;

        public bool IsKnockedDown => _state.Value.Active;

        private readonly NetworkVariable<KnockdownState> _state = new NetworkVariable<KnockdownState>();

        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private readonly List<Collider> _disabledColliders = new List<Collider>();

        private GameObject _spawnedRagdoll;
        private PlayerRagdoll _ragdoll;
        private Coroutine _recoverRoutine;
        private bool _wasKinematic;
        private bool _bodyHidden;

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += Knockdown_OnStateChanged;

            if (playerDead != null)
            {
                playerDead.OnDeathEvent += PlayerDead_OnDeathEvent;
            }

            // A late joiner can arrive with someone already on the floor.
            if (_state.Value.Active)
            {
                EnterKnockdown(_state.Value.Impulse);
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= Knockdown_OnStateChanged;

            if (playerDead != null)
            {
                playerDead.OnDeathEvent -= PlayerDead_OnDeathEvent;
            }

            StopRecovering();
            DestroyRagdoll();
        }

        /// <summary>
        /// Server-side entry point for anything that can hit a player — a swinging door today, a
        /// monster or a falling prop tomorrow. The profile decides what the hit actually does.
        /// </summary>
        public void ApplyImpact(ImpactProfileSO profile, Vector3 direction)
        {
            if (!IsServer || profile == null) return;
            if (IsDead()) return;

            if (profile.damage > 0f && playerHealth != null)
            {
                playerHealth.TakeDamage(profile.damage);
            }

            if (!profile.knocksDown) return;

            // The damage above may have killed them, and the dead do not get up.
            if (IsDead()) return;
            if (_state.Value.Active) return;

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            Vector3 impulse = flat.normalized * profile.impulseForce + Vector3.up * profile.upwardForce;

            _state.Value = new KnockdownState { Active = true, Impulse = impulse };

            StopRecovering();
            _recoverRoutine = StartCoroutine(RecoverAfter(profile.knockdownSeconds));
        }

        private IEnumerator RecoverAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));

            _recoverRoutine = null;

            if (IsDead()) yield break;

            _state.Value = new KnockdownState { Active = false, Impulse = Vector3.zero };
        }

        private void Knockdown_OnStateChanged(KnockdownState previous, KnockdownState current)
        {
            if (current.Active)
            {
                EnterKnockdown(current.Impulse);
            }
            else
            {
                ExitKnockdown();
            }
        }

        private void EnterKnockdown(Vector3 impulse)
        {
            if (_spawnedRagdoll != null) return;

            // Flag the camera first: the input lock below is the same one menus use, and without
            // this it would show the cursor and demote this camera out of the way.
            if (IsOwner && playerCamera != null)
            {
                playerCamera.SetKnockedDown(true);
            }

            // Lock before freezing: PlayerMovement.StopMovement zeroes the velocities, and writing
            // those on an already-kinematic body is not allowed.
            if (IsOwner)
            {
                playerState.SetInputLocked(true);
            }

            SpawnRagdoll(impulse);
            HideLivingBody();
            FreezeBody(true);

            if (!IsOwner || _ragdoll == null) return;

            if (playerCameraOffset != null)
            {
                playerCameraOffset.AttachRagdollCamera(_ragdoll.HeadBone, _ragdoll.EyesForward);
            }
        }

        private void ExitKnockdown()
        {
            // Read the ragdoll before it is destroyed — it is what says where we ended up.
            if (IsOwner)
            {
                MoveToStandingSpot();

                if (playerCameraOffset != null)
                {
                    playerCameraOffset.DetachRagdollCamera();
                }
            }

            FreezeBody(false);
            RestoreLivingBody();
            DestroyRagdoll();

            if (!IsOwner) return;

            // Clear the flag before unlocking, so PlayerCamera runs its normal restore path and
            // recaptures the cursor and the camera priority.
            if (playerCamera != null)
            {
                playerCamera.SetKnockedDown(false);
            }

            playerState.SetInputLocked(false);
        }

        private void MoveToStandingSpot()
        {
            if (_ragdoll == null || _ragdoll.HipsBone == null) return;

            Vector3 origin = _ragdoll.HipsBone.position;

            Vector3 scale = transform.lossyScale;
            float radius = playerCapsule != null ? playerCapsule.radius * Mathf.Max(scale.x, scale.z) : 0.4f;
            float height = playerCapsule != null ? playerCapsule.height * scale.y : 1.8f;

            if (!StandingSpotFinder.TryFind(origin, radius, height, standingBlockingMask,
                    searchMaxDistance, searchStepSize, searchAngleStep, out Vector3 spot))
            {
                Debug.LogWarning($"PlayerKnockdown: no free standing spot within {searchMaxDistance}m of {origin}; getting up where the body landed.");
                spot = origin;
            }

            Teleport(spot + Vector3.up * groundOffset);
        }

        private void Teleport(Vector3 position)
        {
            if (playerRigidbody != null)
            {
                // A kinematic body refuses velocity writes; position still teleports it.
                if (!playerRigidbody.isKinematic)
                {
                    playerRigidbody.linearVelocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                }

                playerRigidbody.position = position;
            }

            transform.position = position;
            Physics.SyncTransforms();
        }

        private void SpawnRagdoll(Vector3 impulse)
        {
            if (ragdollPrefab == null) return;

            Transform spawnRoot = ragdollSpawnRoot != null ? ragdollSpawnRoot : transform;
            _spawnedRagdoll = Instantiate(ragdollPrefab, spawnRoot.position, spawnRoot.rotation);

            if (_spawnedRagdoll.TryGetComponent(out _ragdoll))
            {
                _ragdoll.InitializeFrom(spawnRoot);
                _ragdoll.ApplyImpulse(impulse);
            }
        }

        private void DestroyRagdoll()
        {
            if (_spawnedRagdoll != null)
            {
                Destroy(_spawnedRagdoll);
            }

            _spawnedRagdoll = null;
            _ragdoll = null;
        }

        private void FreezeBody(bool frozen)
        {
            if (playerRigidbody == null) return;

            if (frozen)
            {
                _wasKinematic = playerRigidbody.isKinematic;
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
                playerRigidbody.isKinematic = true;
            }
            else
            {
                playerRigidbody.isKinematic = _wasKinematic;
            }
        }

        private void HideLivingBody()
        {
            if (_bodyHidden) return;

            _bodyHidden = true;

            if (playerAnimator != null)
            {
                playerAnimator.enabled = false;
            }

            // Only touch what is currently on, so getting up cannot switch something back on that
            // was deliberately off (an occlusion renderer, a disabled trigger).
            _hiddenRenderers.Clear();
            foreach (Renderer playerRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!playerRenderer.enabled) continue;

                playerRenderer.enabled = false;
                _hiddenRenderers.Add(playerRenderer);
            }

            _disabledColliders.Clear();
            foreach (Collider playerCollider in GetComponentsInChildren<Collider>(true))
            {
                if (!playerCollider.enabled) continue;

                playerCollider.enabled = false;
                _disabledColliders.Add(playerCollider);
            }
        }

        private void RestoreLivingBody()
        {
            if (!_bodyHidden) return;

            _bodyHidden = false;

            foreach (Renderer playerRenderer in _hiddenRenderers)
            {
                if (playerRenderer != null) playerRenderer.enabled = true;
            }

            foreach (Collider playerCollider in _disabledColliders)
            {
                if (playerCollider != null) playerCollider.enabled = true;
            }

            _hiddenRenderers.Clear();
            _disabledColliders.Clear();

            if (playerAnimator != null)
            {
                playerAnimator.enabled = true;
            }
        }

        private void PlayerDead_OnDeathEvent(bool isDead)
        {
            if (!isDead) return;

            // PlayerDead owns the body now: it has already hidden it and spawned its own ragdoll.
            // Drop ours and forget the restore lists so getting up can never undo the death state.
            StopRecovering();

            if (IsOwner)
            {
                if (playerCameraOffset != null) playerCameraOffset.DetachRagdollCamera();
                if (playerCamera != null) playerCamera.SetKnockedDown(false);
            }

            DestroyRagdoll();

            _bodyHidden = false;
            _hiddenRenderers.Clear();
            _disabledColliders.Clear();
        }

        private void StopRecovering()
        {
            if (_recoverRoutine == null) return;

            StopCoroutine(_recoverRoutine);
            _recoverRoutine = null;
        }

        private bool IsDead() => playerDead != null && playerDead.IsDead;
    }
}
