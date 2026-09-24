using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCameraOffset : NetworkBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Transform cameraRoot;

        [SerializeField] private float cameraMoveSpeed = 10f;
        [SerializeField] private Vector3 standingOffset;
        [SerializeField] private Vector3 crouchOffset;
        [SerializeField] private Vector3 runOffset;
        [SerializeField] private Vector3 deadOffset;

        [Header("Clipping while down")]
        [Tooltip("What the camera must stay out of while it rides the ragdoll. Empty means every " +
                 "layer except players, ragdolls and the non-solid ones.")]
        [SerializeField] private LayerMask fallenClipLayers;

        [Tooltip("How close the camera may come to a surface while it rides the ragdoll.")]
        [SerializeField, Min(0.01f)] private float fallenClearance = 0.2f;

        // A hard landing sinks the head this far into the floor for a few frames; the floor probe
        // starts above it so it still finds the surface from the right side.
        private const float SinkProbeHeight = 0.4f;

        private Vector3 _targetCameraOffset;
        private Transform _originalParent;
        private bool _isRunning;
        private bool _isCrouching;
        private bool _isDead;
        private Transform _deathCameraBone;

        private bool _ragdollAttached;
        private Quaternion _ragdollLookRotation = Quaternion.identity;

        // A point inside the ragdoll's body — its hips — that the camera is cast out from.
        private Transform _clipReference;


        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerState.OnRunEvent += PlayerState_OnRunEvent;
                playerState.OnCrouchEvent += PlayerState_OnCrouchEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;

                _targetCameraOffset = standingOffset;
                cameraRoot.localPosition = standingOffset;
                _originalParent = cameraRoot.parent;

                if (fallenClipLayers.value == 0)
                {
                    fallenClipLayers = ~LayerMask.GetMask("Player", "DeadPlayer", "Body", "OcclusionBody",
                        "Ignore Raycast", "UI", "TransparentFX");
                }
            }
        }


        private void LateUpdate()
        {
            if (!IsOwner) return;

            float t = cameraMoveSpeed * Time.deltaTime;

            if (_ragdollAttached)
            {
                // Parented to the head bone, so both of these are in bone space. The offset has to
                // ease to zero and not to standingOffset: the Armature is scaled 100x, and one
                // metre of offset expressed in bone space is a hundred metres of camera.
                cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, Vector3.zero, t);
                cameraRoot.localRotation = Quaternion.Slerp(cameraRoot.localRotation, _ragdollLookRotation, t);

                KeepClearOfGeometry();
                return;
            }

            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, _targetCameraOffset, t);
            cameraRoot.localRotation = Quaternion.Slerp(cameraRoot.localRotation, Quaternion.identity, t);

            if (_isDead && _deathCameraBone != null)
            {
                KeepClearOfGeometry();
            }
        }

        /// <summary>
        /// Parents the camera to the ragdoll's head, exactly like the death camera, and aims it
        /// down the character's gaze. <paramref name="eyesForward"/> is a point in front of the
        /// eyes parented to the same bone, so its local position IS the gaze direction in bone
        /// space — which makes the look rotation a constant, not per-frame work.
        ///
        /// The rotation only reaches the view because PlayerCamera flips PanTilt to ParentObject
        /// with zeroed axes for the duration; in World it would be ignored.
        /// </summary>
        /// <param name="clipReference">A point inside the body — the hips — to keep the camera clear of walls from.</param>
        public void AttachRagdollCamera(Transform headBone, Transform eyesForward, Transform clipReference = null)
        {
            if (!IsOwner || headBone == null) return;

            // worldPositionStays keeps the camera where it is for the ease-in, and it is also what
            // compensates the Armature's 100x scale into cameraRoot's localScale.
            cameraRoot.SetParent(headBone, worldPositionStays: true);
            _ragdollAttached = true;
            _clipReference = clipReference;

            _ragdollLookRotation = eyesForward != null && eyesForward.localPosition.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(eyesForward.localPosition, Vector3.up)
                : Quaternion.identity;
        }

        public void DetachRagdollCamera()
        {
            if (!IsOwner || !_ragdollAttached) return;

            _ragdollAttached = false;
            _clipReference = null;

            cameraRoot.SetParent(_originalParent, worldPositionStays: true);
            cameraRoot.localScale = Vector3.one;

            // Dying while knocked down: the knockdown lets go after the death camera may already
            // have taken over, depending on which listener ran first. Back on the living body, the
            // camera would fall through the floor with it — that body has no colliders once dead.
            if (_isDead && _deathCameraBone != null)
            {
                AttachCameraTo(_deathCameraBone);
            }
        }

        /// <summary>
        /// Keeps the camera out of the floor and the walls while it rides the ragdoll.
        ///
        /// <para>The head bone sits right at the skin, and a hard landing sinks it into the floor
        /// for a moment — the lens clips at a centimetre, so the world showed from underneath.
        /// The view still follows the eyes; only the position is corrected. First it is cast out
        /// from the hips towards the head, so a wall or a door the head is pressed against stops
        /// it short. Then it is lifted clear of the floor, probed from above so a head that has
        /// already sunk below the surface still finds it.</para>
        /// </summary>
        private void KeepClearOfGeometry()
        {
            Vector3 desired = cameraRoot.position;
            Vector3 safe = desired;

            if (_clipReference != null)
            {
                Vector3 from = _clipReference.position;
                Vector3 toCamera = desired - from;
                float distance = toCamera.magnitude;

                if (distance > 0.001f)
                {
                    Vector3 direction = toCamera / distance;

                    if (Physics.SphereCast(from, fallenClearance * 0.75f, direction, out RaycastHit wall, distance,
                            fallenClipLayers, QueryTriggerInteraction.Ignore))
                    {
                        safe = from + direction * wall.distance;
                    }
                }
            }

            Vector3 probe = safe + Vector3.up * SinkProbeHeight;

            if (Physics.Raycast(probe, Vector3.down, out RaycastHit floor, SinkProbeHeight + fallenClearance,
                    fallenClipLayers, QueryTriggerInteraction.Ignore))
            {
                safe.y = Mathf.Max(safe.y, floor.point.y + fallenClearance);
            }

            if (safe != desired)
            {
                cameraRoot.position = safe;
            }
        }

        private void PlayerState_OnRunEvent(bool isRunning)
        {
            _isRunning = isRunning;
            UpdateCameraOffset();
        }

        private void PlayerState_OnCrouchEvent(bool isCrouching)
        {
            _isCrouching = isCrouching;
            UpdateCameraOffset();
        }

        public void SetDeathCameraBone(Transform bone)
        {
            _deathCameraBone = bone;

            if (_isDead && _deathCameraBone != null)
            {
                AttachCameraTo(_deathCameraBone);
            }
        }

        /// <summary>Rides a ragdoll bone instead of the player body. Used by death and by knockdowns.</summary>
        public void AttachCameraTo(Transform bone)
        {
            if (!IsOwner || bone == null) return;

            cameraRoot.SetParent(bone, worldPositionStays: true);

            PlayerRagdoll ragdoll = bone.GetComponentInParent<PlayerRagdoll>();
            _clipReference = ragdoll != null ? ragdoll.HipsBone : null;
        }

        /// <summary>Puts the camera back on the player body.</summary>
        public void DetachCamera()
        {
            if (!IsOwner) return;

            cameraRoot.SetParent(_originalParent, worldPositionStays: true);
            _clipReference = null;
        }

        private void PlayerState_OnPlayerDead(bool isDead)
        {
            _isDead = isDead;

            if (_isDead && _deathCameraBone != null)
            {
                AttachCameraTo(_deathCameraBone);
            }
            else
            {
                DetachCamera();
                _deathCameraBone = null;
            }

            UpdateCameraOffset();
        }

        private void UpdateCameraOffset()
        {
            if (_isDead)
            {
                _targetCameraOffset = deadOffset;
            }
            else if (_isRunning)
            {
                _targetCameraOffset = runOffset;
            }
            else if (_isCrouching)
            {
                _targetCameraOffset = crouchOffset;
            }
            else
            {
                _targetCameraOffset = standingOffset;
            }
        }


        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerState.OnRunEvent -= PlayerState_OnRunEvent;
                playerState.OnCrouchEvent -= PlayerState_OnCrouchEvent;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
            }
        }

    }

}
