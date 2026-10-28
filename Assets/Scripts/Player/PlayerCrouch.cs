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

        [Header("Squashed hat collider settings")]
        [Tooltip("The crouched capsule once the hat is flattened: same floor and radius, lower top. " +
                 "A height of 0 turns the squash off.")]
        [SerializeField] private Vector3 squashedColliderCenter;
        [SerializeField] private float squashedHeight;

        [Tooltip("How far past the crouched capsule a ceiling is looked for, so the hat is already " +
                 "down by the time the player reaches it.")]
        [SerializeField, Min(0f)] private float hatProbeReach = 0.2f;

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

        // Same deal as _isCrouched: the owner decides, every copy of the capsule follows, and the
        // hat's blend shape reads it on every peer.
        private readonly NetworkVariable<bool> _isHatSquashed = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool IsCrouching => _isCrouched.Value;
        public bool IsHatSquashed => _isHatSquashed.Value;

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
                SnapCollider();
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
                UpdateHatState();
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

        /// <summary>
        /// Flattens the hat when it is what is in the way: a ceiling between the squashed top and
        /// the crouched top, over the capsule or just ahead of it. It only stands back up once the
        /// crouched capsule fits again, so it never pops up into the underside of a table.
        /// </summary>
        private void UpdateHatState()
        {
            bool isSquashed = _isCrouched.Value && squashedHeight > 0f &&
                              (IsHatBlocked() || (_isHatSquashed.Value && !CrouchedCapsuleFits()));

            if (isSquashed == _isHatSquashed.Value) return;

            _isHatSquashed.Value = isSquashed;
        }

        /// <summary>
        /// Rays straight up through the band only the hat occupies, from the middle of the capsule
        /// and a ring just outside it. A wall the ring starts inside is not hit from within, and one
        /// beside it runs parallel, so only something overhead - a table top, a vent's lip - counts.
        /// </summary>
        private bool IsHatBlocked()
        {
            Transform capsuleTransform = capsuleCollider.transform;

            float squashedTop = squashedColliderCenter.y + squashedHeight * 0.5f;
            float crouchedTop = crouchColliderCenter.y + crouchHeight * 0.5f;
            float startY = squashedTop - ceilingCheckSkin;
            float length = crouchedTop - startY + ceilingCheckSkin;
            float ring = crouchRadius + hatProbeReach;

            for (int i = 0; i <= HatProbeRingCount; i++)
            {
                Vector3 local = new Vector3(crouchColliderCenter.x, startY, crouchColliderCenter.z);

                if (i > 0)
                {
                    float angle = (i - 1) * Mathf.PI * 2f / HatProbeRingCount;
                    local += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                }

                if (Physics.Raycast(capsuleTransform.TransformPoint(local), capsuleTransform.up, length,
                        ceilingMask, QueryTriggerInteraction.Ignore))
                {
                    return true;
                }
            }

            return false;
        }

        private const int HatProbeRingCount = 8;

        private bool CrouchedCapsuleFits()
        {
            GetCheckCapsule(crouchColliderCenter, crouchHeight, crouchRadius, out Vector3 bottom, out Vector3 top,
                out float radius);
            return !Physics.CheckCapsule(bottom, top, radius, ceilingMask, QueryTriggerInteraction.Ignore);
        }

        private void GetColliderTarget(out Vector3 center, out float height, out float radius)
        {
            if (!_isCrouched.Value)
            {
                center = _standColliderCenter;
                height = _standHeight;
                radius = _standColliderRadius;
                return;
            }

            bool isSquashed = _isHatSquashed.Value && squashedHeight > 0f;
            center = isSquashed ? squashedColliderCenter : crouchColliderCenter;
            height = isSquashed ? squashedHeight : crouchHeight;
            radius = crouchRadius;
        }

        private void UpdateCollider()
        {
            GetColliderTarget(out Vector3 targetCenter, out float targetHeight, out float targetRadius);

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
                SnapCollider();
            }
        }

        private void SnapCollider()
        {
            GetColliderTarget(out Vector3 targetCenter, out float targetHeight, out float targetRadius);
            capsuleCollider.height = targetHeight;
            capsuleCollider.center = targetCenter;
            capsuleCollider.radius = targetRadius;
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

            GetCheckCapsule(center, height, standRadius, out bottom, out top, out radius);
        }

        private void GetCheckCapsule(Vector3 center, float height, float baseRadius, out Vector3 bottom,
            out Vector3 top, out float radius)
        {
            radius = Mathf.Max(0.01f, baseRadius - ceilingCheckSkin);
            float halfSegment = Mathf.Max(0f, height * 0.5f - baseRadius);

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

