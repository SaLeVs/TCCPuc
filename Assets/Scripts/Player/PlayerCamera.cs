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
        [SerializeField] private CinemachineInputAxisController inputAxisController;
        [SerializeField] private CinemachinePanTilt panTilt;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform orientation;
        [SerializeField] private Renderer[] occlusionRenderers;

        [SerializeField] private int ownerCameraPriority = 10;

        public CinemachineCamera playerCinemachineCamera => cinemachineCamera;

        private bool _isDead;
        private bool _isLocked;
        private bool _isPaused;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                cinemachineCamera.Priority = ownerCameraPriority;
                inputReader.OnPauseEvent += ToggleMouse;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;

                inputAxisController.enabled = true;
                LockMouse();
                HideOcclusionRenderers();
            }
            else
            {
                inputAxisController.enabled = false;
            }
        }

        public void SetSpectatorMode(bool isSpectating)
        {
            cinemachineCamera.Priority = isSpectating ? 0 : ownerCameraPriority;
        }

        private void ToggleMouse()
        {
            _isPaused = !_isPaused;
            inputAxisController.enabled = !_isPaused && !_isLocked;

            if (_isPaused) UnlockMouse();
            else LockMouse();

            OnPauseToggled?.Invoke(_isPaused);
        }

        public void SetPauseState(bool isPaused)
        {
            _isPaused = isPaused;
            inputAxisController.enabled = !_isPaused && !_isLocked;
            OnPauseToggled?.Invoke(isPaused);
        }

        private void PlayerState_OnPlayerDead(bool isDead) => _isDead = isDead;

        private void PlayerState_OnPlayerLocked(bool locked)
        {
            _isLocked = locked;
            inputAxisController.enabled = !locked && !_isPaused && !_isDead;

            if (locked)
            {
                UnlockMouse();
                cinemachineCamera.Priority = 0;
            }
            else
            {
                if (_isPaused) UnlockMouse();
                else LockMouse();
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
                currentRenderer.enabled = false;
        }

        public void SetOcclusionRenderersVisible(bool visible)
        {
            foreach (Renderer currentRenderer in occlusionRenderers)
                currentRenderer.enabled = visible;
        }

        private void LateUpdate()
        {
            if (IsOwner && !_isDead && !_isLocked)
            {
                SyncBodyRotationWithCamera();
            }
        }
        
        private void SyncBodyRotationWithCamera()
        {
            float yaw = panTilt.PanAxis.Value;
            orientation.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnPauseEvent -= ToggleMouse;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;

                cinemachineCamera.Priority = 0;
            }
        }
    }
}