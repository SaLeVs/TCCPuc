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
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Renderer[] occlusionRenderers;

        [SerializeField] private int ownerCameraPriority = 10;
        [SerializeField] private float minSensitivityMultiplier = 0.3f;
        [SerializeField] private float maxSensitivityMultiplier = 3f;

        public CinemachineCamera playerCinemachineCamera => cinemachineCamera;

        private bool _isDead;
        private bool _isLocked;
        private bool _isPaused;
        private bool _isKnockedDown;
        private CinemachinePanTilt.ReferenceFrames _savedReferenceFrame;
        private float _savedPan;
        private float _savedTilt;
        private const string SENSIBILITY_KEY = "MouseSensibility";
        
        private float _baseLookXGain;
        private float _baseLookYGain;
        private float _yaw;


        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                cinemachineCamera.Priority = ownerCameraPriority;
                inputReader.OnPauseEvent += ToggleMouse;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;

                inputAxisController.enabled = true;

                CacheBaseSensitivityGains();
                ApplySensitivity(SensibilitySettings.Current);
                SensibilitySettings.OnSensibilityChanged += ApplySensitivity;

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

        /// <summary>
        /// A knockdown locks input the same way a menu does, but it is not a menu: the player is
        /// still looking at the world, so the cursor stays captured and this camera stays live.
        /// </summary>
        public void SetKnockedDown(bool knockedDown)
        {
            if (_isKnockedDown == knockedDown) return;

            _isKnockedDown = knockedDown;

            if (knockedDown)
            {
                // PanTilt aims in World, which means CameraRoot's rotation never reaches the view.
                // Point it at the parent and zero the axes instead: the camera then looks exactly
                // down CameraRoot's forward, which is what lets PlayerCameraOffset aim it along
                // the ragdoll's gaze.
                _savedReferenceFrame = panTilt.ReferenceFrame;
                _savedPan = panTilt.PanAxis.Value;
                _savedTilt = panTilt.TiltAxis.Value;

                panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.ParentObject;
                panTilt.PanAxis.Value = 0f;
                panTilt.TiltAxis.Value = 0f;
                return;
            }

            panTilt.ReferenceFrame = _savedReferenceFrame;
            panTilt.PanAxis.Value = _savedPan;
            panTilt.TiltAxis.Value = _savedTilt;
        }

        private void PlayerState_OnPlayerLocked(bool locked)
        {
            _isLocked = locked;
            inputAxisController.enabled = !locked && !_isPaused && !_isDead;

            if (_isKnockedDown) return;

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
                _yaw = panTilt.PanAxis.Value;
                orientation.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
        }

        private void FixedUpdate()
        {
            if (!IsOwner || _isDead || _isLocked) return;

            Quaternion bodyRotation = Quaternion.Euler(0f, _yaw, 0f);
            
            if (rb != null)
            {
                rb.MoveRotation(bodyRotation);
            }
            else
            {
                transform.rotation = bodyRotation;
            }
        }
        
        private void CacheBaseSensitivityGains()
        {
            foreach (var controller in inputAxisController.Controllers)
            {
                if (controller.Name.Contains("Pan")) _baseLookXGain = controller.Input.Gain;
                else if (controller.Name.Contains("Tilt")) _baseLookYGain = controller.Input.Gain;
            }
        }

        private void ApplySensitivity(float normalizedSensitivity)
        {
            float multiplier = Mathf.Lerp(minSensitivityMultiplier, maxSensitivityMultiplier, normalizedSensitivity);

            foreach (var controller in inputAxisController.Controllers)
            {
                if (controller.Name.Contains("Pan")) controller.Input.Gain = _baseLookXGain * multiplier;
                else if (controller.Name.Contains("Tilt")) controller.Input.Gain = _baseLookYGain * multiplier;
            }
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnPauseEvent -= ToggleMouse;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
                SensibilitySettings.OnSensibilityChanged -= ApplySensitivity;

                cinemachineCamera.Priority = 0;
            }
        }
        
    }
}