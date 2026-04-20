using System;
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
        private bool _isRunning;
        private bool _isCrouching;
        private bool _isDead;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerState.OnRunEvent += PlayerState_OnRunEvent;
                playerState.OnCrouchEvent += PlayerState_OnCrouchEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                
                _targetCameraOffset = standingOffset;
                cameraRoot.localPosition = standingOffset;
            }
        }
        

        private void LateUpdate()
        {
            if (IsOwner)
            {
                cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, _targetCameraOffset, cameraMoveSpeed * Time.deltaTime);
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
        
        private void PlayerState_OnPlayerDead(bool isDead)
        {
            _isDead = isDead;
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
