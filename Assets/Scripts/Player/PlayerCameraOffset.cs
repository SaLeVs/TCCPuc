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
        
        private Vector3 _targetCameraOffset;
        private bool _isRunning;
        private bool _isCrouching;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerState.OnRunEvent += PlayerState_OnRunEvent;
                playerState.OnCrouchEvent += PlayerState_OnCrouchEvent;
                
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
        
        private void UpdateCameraOffset()
        {
            if (_isCrouching)
            {
                _targetCameraOffset = crouchOffset;
            }
            else if (_isRunning)
            {
                _targetCameraOffset = runOffset;
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
            }
        }
        
    }

}
