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
        [SerializeField] private float lerpTime = 20f;
        
        
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform orientation;
        [SerializeField] private PlayerMovement playerMovement;
        
        private Vector2 _cameraLookInput;
        private float _xRotation;
        private float _yRotation;
        private float _visualYaw;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                cinemachineCamera.Priority = ownerCameraPriority;
                inputReader.OnCameraLookEvent += InputReader_OnCameraLookEvent;
            }
        }
        
        private void InputReader_OnCameraLookEvent(Vector2 cameraLookInput) => _cameraLookInput = cameraLookInput;
        
        private void LateUpdate()
        {
            if (IsOwner)
            {
                SmoothVisualYaw();
                CameraMovement();
            }
        }
        
        private void SmoothVisualYaw()
        {
            float targetYaw = playerMovement.SimulationYaw;

            _visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, lerpTime * Time.deltaTime);
        }

        
        private void CameraMovement()
        {
            _xRotation -= _cameraLookInput.y * sensitivity * Time.deltaTime;
            _xRotation = Mathf.Clamp(_xRotation, upperClamp, lowerClamp);

            cameraRoot.rotation = Quaternion.Euler(_xRotation, _visualYaw, 0f);

            cinemachineCamera.transform.SetPositionAndRotation(cameraRoot.position, cameraRoot.rotation);
        }
    
    }
}


