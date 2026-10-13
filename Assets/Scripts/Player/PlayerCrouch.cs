using System;
using Inputs;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCrouch : NetworkBehaviour, ISpeedModifier
    {
        public event Action<bool> OnCrouchEvent;

        [SerializeField] private PlayerState playerState;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private CapsuleCollider capsuleCollider;
        [SerializeField] private float speedModifier = 0.5f;

        [Header("Crouch collider settings")]
        [SerializeField] private Vector3 crouchColliderCenter;
        [SerializeField] private float crouchRadius;
        [SerializeField] private float crouchHeight;

        [SerializeField] private float crouchSpeed;

        [Header("Ceiling check")]
        [Tooltip("What keeps a crouched player from standing up. The player's own layer must stay out of it.")]
        [SerializeField] private LayerMask ceilingMask;

        [Tooltip("How far the standing capsule is shrunk for the check. The bottom is lifted twice this, " +
                 "so the floor under the feet never reads as a ceiling.")]
        [SerializeField, Min(0.001f)] private float ceilingCheckSkin = 0.05f;

        // Owner writes, everyone applies it to their own copy of the capsule. The server is who
        // runs the monster's vision against that capsule, so it has to shrink there too.
        private readonly NetworkVariable<bool> _isCrouched = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool IsCrouching => _isCrouched.Value;

        private Vector3 _standColliderCenter;
        private float _standHeight;
        private float _standColliderRadius;

        private bool _isCrouchHeld;
        private bool _isDead;
        private bool _isLocked;


        private void Awake()
        {
            _standColliderCenter = capsuleCollider.center;
            _standHeight = capsuleCollider.height;
            _standColliderRadius = capsuleCollider.radius;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnCrouchEvent += InputReader_OnCrouchEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            }
            else if (_isCrouched.Value)
            {
                // Joined while this player was already down: no reason to watch them sink.
                SnapCollider(true);
            }
        }


        private void PlayerState_OnPlayerDead(bool isDead) => _isDead = isDead;

        private void PlayerState_OnPlayerLocked(bool isLocked)
        {
            _isLocked = isLocked;

            // The release may have happened while locked, or the key may still be down now —
            // either way the last event is stale, so ask the input directly.
            if (!_isLocked)
            {
                _isCrouchHeld = inputReader.IsCrouchHeld;
            }
        }

        private void InputReader_OnCrouchEvent(bool isCrouchHeld) => _isCrouchHeld = isCrouchHeld;

        private void Update()
        {
            if (IsOwner)
            {
                UpdateCrouchState();
            }

            UpdateCollider();
        }

        /// <summary>
        /// Dying stands the body up; being locked only drops the input, so a player locked while
        /// under a table or inside a vent stays crouched until there is room.
        /// </summary>
        private void UpdateCrouchState()
        {
            bool wantsCrouch = _isCrouchHeld && !_isLocked && !_isDead;
            bool isCrouched = !_isDead && (wantsCrouch || (_isCrouched.Value && IsCeilingBlocked()));

            if (isCrouched == _isCrouched.Value) return;

            _isCrouched.Value = isCrouched;
            OnCrouchEvent?.Invoke(isCrouched);
        }

        private void UpdateCollider()
        {
            bool isCrouched = _isCrouched.Value;
            float targetHeight = isCrouched ? crouchHeight : _standHeight;
            Vector3 targetCenter = isCrouched ? crouchColliderCenter : _standColliderCenter;
            float targetRadius = isCrouched ? crouchRadius : _standColliderRadius;

            // Every write to the collider rebuilds its physics shape; once settled, leave it alone.
            if (Mathf.Approximately(capsuleCollider.height, targetHeight) &&
                Mathf.Approximately(capsuleCollider.radius, targetRadius) &&
                capsuleCollider.center == targetCenter)
            {
                return;
            }

            float t = crouchSpeed * Time.deltaTime;
            capsuleCollider.height = Mathf.Lerp(capsuleCollider.height, targetHeight, t);
            capsuleCollider.center = Vector3.Lerp(capsuleCollider.center, targetCenter, t);
            capsuleCollider.radius = Mathf.Lerp(capsuleCollider.radius, targetRadius, t);

            if (Mathf.Abs(capsuleCollider.height - targetHeight) < 0.01f)
            {
                SnapCollider(isCrouched);
            }
        }

        private void SnapCollider(bool isCrouched)
        {
            capsuleCollider.height = isCrouched ? crouchHeight : _standHeight;
            capsuleCollider.center = isCrouched ? crouchColliderCenter : _standColliderCenter;
            capsuleCollider.radius = isCrouched ? crouchRadius : _standColliderRadius;
        }

        /// <summary>
        /// Whether the standing capsule would fit where the player is. A single ray from the head
        /// missed beams and edges off-centre, and followed the animation pose of the bone it hung
        /// from; the whole standing volume answers the real question.
        /// </summary>
        private bool IsCeilingBlocked()
        {
            GetCeilingCheckCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            return Physics.CheckCapsule(bottom, top, radius, ceilingMask, QueryTriggerInteraction.Ignore);
        }

        private void GetCeilingCheckCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            Vector3 center = Application.isPlaying ? _standColliderCenter : capsuleCollider.center;
            float height = Application.isPlaying ? _standHeight : capsuleCollider.height;
            float standRadius = Application.isPlaying ? _standColliderRadius : capsuleCollider.radius;

            radius = Mathf.Max(0.01f, standRadius - ceilingCheckSkin);
            float halfSegment = Mathf.Max(0f, height * 0.5f - standRadius);

            // Lifting the bottom sphere by the skin, on top of the smaller radius, keeps the
            // capsule's lowest point 2x skin above the feet. The top keeps the standing height.
            Transform capsuleTransform = capsuleCollider.transform;
            bottom = capsuleTransform.TransformPoint(center + Vector3.down * (halfSegment - ceilingCheckSkin));
            top = capsuleTransform.TransformPoint(center + Vector3.up * (halfSegment + ceilingCheckSkin));
        }

        public float ModifySpeed(float baseSpeed)
        {
            if (_isCrouched.Value)
            {
                return baseSpeed * speedModifier;
            }

            return baseSpeed;

        }

        private void OnDrawGizmosSelected()
        {
            if (capsuleCollider == null) return;

            GetCeilingCheckCapsule(out Vector3 bottom, out Vector3 top, out float radius);

            Gizmos.color = Color.darkRed;
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawLine(bottom, top);
        }


        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnCrouchEvent -= InputReader_OnCrouchEvent;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
            }
        }

    }
}

