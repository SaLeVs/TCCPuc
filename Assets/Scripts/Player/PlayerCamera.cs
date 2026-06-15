using System;
using Inputs;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCamera : NetworkBehaviour
    {
        public event Action<bool> OnPauseToggled;
        
        [SerializeField] private PlayerState playerState;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform orientation;
        [SerializeField] private Renderer[] occlusionRenderers;
        
        [SerializeField] private int ownerCameraPriority = 10;
        [SerializeField] private float upperClamp = -40f;
        [SerializeField] private float lowerClamp = 70f;
        [SerializeField] private float sensitivity = 20f;
        
        public CinemachineCamera playerCinemachineCamera => cinemachineCamera;
        
        private Vector2 _lookInput;

        private float _yaw;
        private float _pitch;
        
        private bool _isDead;
        private bool _isLocked;
        private bool _isPaused;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                cinemachineCamera.Priority = ownerCameraPriority;
                inputReader.OnCameraLookEvent += InputReader_OnCameraLookEvent;
                inputReader.OnPauseEvent += ToggleMouse;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
                
                LockMouse();
                HideOcclusionRenderers();
            } 
        }

        private void InputReader_OnCameraLookEvent(Vector2 cameraLookInput) => _lookInput = cameraLookInput;
        

        public void SetSpectatorMode(bool isSpectating)
        {
            cinemachineCamera.Priority = isSpectating ? 0 : ownerCameraPriority;
        }
        
        private void ToggleMouse()
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                UnlockMouse();
            }
            else
            {
                LockMouse();
            }
            
            OnPauseToggled?.Invoke(_isPaused);
        }
        
        private void PlayerState_OnPlayerDead(bool isDead) => _isDead = isDead;
        
        private void PlayerState_OnPlayerLocked(bool locked)
        {
            _isLocked = locked;

            if (locked)
            {
                UnlockMouse();
                cinemachineCamera.Priority = 0;
            }
            else
            {
                if (_isPaused)
                {
                    UnlockMouse();
                }
                else
                {
                    LockMouse();
                }
                cinemachineCamera.Priority = ownerCameraPriority;
            }
        }

        private void LockMouse()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        private void UnlockMouse()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        private void HideOcclusionRenderers()
        {
            foreach (Renderer currentRenderer in occlusionRenderers)
            {
                currentRenderer.enabled = false;
            }
        }
        
        private void LateUpdate()
        {
            if (IsOwner && !_isDead && !_isLocked)
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
                inputReader.OnPauseEvent -= ToggleMouse;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
                
                cinemachineCamera.Priority = 0;
            }
            
        }
    }
}


