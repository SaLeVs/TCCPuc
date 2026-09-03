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
        private Transform _followBone;
        private Quaternion _followRotationOffset = Quaternion.identity;
        
        
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

            if (_followBone != null)
            {
                // Follow in world space instead of parenting: the rig's Armature is scaled 100x, so
                // parenting under a bone drags that scale into cameraRoot (and into the flashlight
                // hanging off it) and turns the offset below into a 100x lever.
                cameraRoot.SetPositionAndRotation(_followBone.position, _followBone.rotation * _followRotationOffset);
                return;
            }

            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, _targetCameraOffset, cameraMoveSpeed * Time.deltaTime);
        }

        /// <summary>Rides a ragdoll bone without reparenting, so no bone scale leaks into the camera.</summary>
        public void FollowRagdollBone(Transform bone)
        {
            if (!IsOwner || bone == null) return;

            // Keep the rotation we already had relative to the bone, so the view tumbles with the
            // body instead of snapping to whatever axis the rig gave that bone.
            _followRotationOffset = Quaternion.Inverse(bone.rotation) * cameraRoot.rotation;
            _followBone = bone;
        }

        /// <summary>Hands the camera back to the player body; LateUpdate eases it home from there.</summary>
        public void StopFollowingRagdollBone()
        {
            if (!IsOwner || _followBone == null) return;

            _followBone = null;
            cameraRoot.localRotation = Quaternion.identity;
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
