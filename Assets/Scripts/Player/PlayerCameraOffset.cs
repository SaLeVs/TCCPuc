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

        private Vector3 _targetCameraOffset;
        private Transform _originalParent;
        private bool _isRunning;
        private bool _isCrouching;
        private bool _isDead;
        private Transform _deathCameraBone;

        private bool _ragdollAttached;
        private Quaternion _ragdollLookRotation = Quaternion.identity;
        
        
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
                return;
            }

            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, _targetCameraOffset, t);
            cameraRoot.localRotation = Quaternion.Slerp(cameraRoot.localRotation, Quaternion.identity, t);
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
        public void AttachRagdollCamera(Transform headBone, Transform eyesForward)
        {
            if (!IsOwner || headBone == null) return;

            // worldPositionStays keeps the camera where it is for the ease-in, and it is also what
            // compensates the Armature's 100x scale into cameraRoot's localScale.
            cameraRoot.SetParent(headBone, worldPositionStays: true);
            _ragdollAttached = true;

            _ragdollLookRotation = eyesForward != null && eyesForward.localPosition.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(eyesForward.localPosition, Vector3.up)
                : Quaternion.identity;
        }

        public void DetachRagdollCamera()
        {
            if (!IsOwner || !_ragdollAttached) return;

            _ragdollAttached = false;

            cameraRoot.SetParent(_originalParent, worldPositionStays: true);
            cameraRoot.localScale = Vector3.one;
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
        }

        /// <summary>Puts the camera back on the player body.</summary>
        public void DetachCamera()
        {
            if (!IsOwner) return;

            cameraRoot.SetParent(_originalParent, worldPositionStays: true);
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
