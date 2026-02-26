using System;
using Inputs;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCamera : NetworkBehaviour
    {
        [SerializeField] private int ownerCameraPriority = 10;
        [SerializeField] private float upperClamp = -40f;
        [SerializeField] private float lowerClamp = 70f;
        [SerializeField] private float sensitivity = 20f;
        
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform playerTransform;
        
        private Vector2 _cameraLookInput;
        private float _xRotation;
        private float _yRotation;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                cinemachineCamera.Priority = ownerCameraPriority;
                inputReader.OnCameraLookEvent += InputReader_OnCameraLookEvent;
            }
        }

        private void LateUpdate()
        {
            if (IsOwner)
            {
                CameraMovement();
            }
        }

        private void InputReader_OnCameraLookEvent(Vector2 cameraLookInput)
        {
            _cameraLookInput = cameraLookInput;
        }

        
        private void CameraMovement()
        {
            cinemachineCamera.transform.position = cameraTransform.position;
            
            _xRotation -= _cameraLookInput.y * sensitivity * Time.deltaTime;
            _xRotation = Mathf.Clamp(_xRotation, upperClamp, lowerClamp);
            
            cinemachineCamera.transform.localRotation = Quaternion.Euler(_xRotation, 0, 0);
            
            playerTransform.Rotate(Vector3.up, _cameraLookInput.x * sensitivity * Time.deltaTime);
        }
    
    }
}


