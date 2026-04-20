using System;
using Inputs;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCamera : NetworkBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform orientation;
        
        [SerializeField] private int ownerCameraPriority = 10;
        [SerializeField] private float upperClamp = -40f;
        [SerializeField] private float lowerClamp = 70f;
        [SerializeField] private float sensitivity = 20f;
        
        private Vector2 _lookInput;

        private float _yaw;
        private float _pitch;
        
        private bool _isDead;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                cinemachineCamera.Priority = ownerCameraPriority;
                inputReader.OnCameraLookEvent += InputReader_OnCameraLookEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
            }
            
        }


        private void InputReader_OnCameraLookEvent(Vector2 cameraLookInput) => _lookInput = cameraLookInput;
        private void PlayerState_OnPlayerDead(bool isDead) => _isDead = isDead;
        
        private void LateUpdate()
        {
            if (IsOwner && !_isDead)
            {   
                CameraMovement();
            }
            
        }
        
        private void CameraMovement()
        {
            float deltaTime = Time.deltaTime;
            
            _yaw += _lookInput.x * sensitivity * deltaTime;
            
            _pitch -= _lookInput.y * sensitivity * deltaTime;
            _pitch = Mathf.Clamp(_pitch, upperClamp, lowerClamp);
            
            orientation.rotation = Quaternion.Euler(0f, _yaw, 0f);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            
        }
        
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnCameraLookEvent -= InputReader_OnCameraLookEvent;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                cinemachineCamera.Priority = 0;
            }
            
        }
    }
}


