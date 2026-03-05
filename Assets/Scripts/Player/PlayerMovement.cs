using System;
using System.Collections.Generic;
using Components;
using UnityEngine;
using Inputs;
using Network;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Transform orientation;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float runSpeed;
        [SerializeField] private float blendMovementTime = 8.9f;
        
        [SerializeField] private float sensitivity = 20f;
        [SerializeField] private float staminaMax;
        [SerializeField] private float staminaDrainPerSecond;
        [SerializeField] private float staminaGainPerSecond;
        [SerializeField] private float staminaCooldownThreshold = 5f;
        
        
        public float SimulationYaw => _simulationYaw; 
        
        private Vector2 _movementInput;
        private Vector2 _cameraLookInput;
        private bool _isRunning;
        
        private float _simulationYaw;  
        private float _visualYaw;
        
        private Vector3 _movementDirection;
        private float _targetSpeed;
        
        private Vector2 _currentVelocity;
        private float _xVelocityDifference;
        private float _zVelocityDifference;
        
        private float _stamina;
        private bool _isExhausted;
        
        
        
        public override void OnNetworkSpawn()
        {
            _stamina = staminaMax;
            
            if (IsOwner)
            {
                inputReader.OnMoveEvent += InputReader_OnMoveEvent;
                inputReader.OnCameraLookEvent += InputReader_OnCameraLookEvent;
                inputReader.OnRunEvent += InputReader_OnRunEvent;
                
            }
            
        }
        
        private void InputReader_OnMoveEvent(Vector2 movementInput) => _movementInput = movementInput;
        private void InputReader_OnCameraLookEvent(Vector2 cameraLookInput) => _cameraLookInput = cameraLookInput;
        private void InputReader_OnRunEvent(bool isRunning) => _isRunning = isRunning;

        private void Update()
        {
            if (IsOwner)
            {
                UpdateStamina(_isRunning);
                
            }
            
        }
        
        private void FixedUpdate()
        {
            if (IsOwner)
            {
                Move(_movementInput);
                Look(_cameraLookInput);
                
            }
            
        }
        
        
        private void Move(Vector2 moveVector)
        {
            bool canRun = _isRunning && !_isExhausted;

            _targetSpeed = canRun ? runSpeed : moveSpeed;
            
            Vector3 desiredVelocityWorld = Vector3.zero;
            
            if (moveVector.sqrMagnitude > 0.0001f)
            {
                desiredVelocityWorld = orientation.forward * moveVector.y + orientation.right * moveVector.x;
                desiredVelocityWorld.y = 0f;
                float inputMag = desiredVelocityWorld.magnitude;
                
                if (inputMag > 0.0001f)
                {
                    desiredVelocityWorld = desiredVelocityWorld.normalized * (_targetSpeed * Mathf.Clamp01(moveVector.magnitude));
                }
                else
                {
                    desiredVelocityWorld = Vector3.zero;
                }
            }
            else
            {
                desiredVelocityWorld = Vector3.zero;
            }
            
            _currentVelocity.x = Mathf.Lerp(_currentVelocity.x, desiredVelocityWorld.x, blendMovementTime * Time.fixedDeltaTime);
            _currentVelocity.y = Mathf.Lerp(_currentVelocity.y, desiredVelocityWorld.z, blendMovementTime * Time.fixedDeltaTime);
            
            _xVelocityDifference = _currentVelocity.x - rb.linearVelocity.x;
            _zVelocityDifference = _currentVelocity.y - rb.linearVelocity.z;

            //  float lerpFraction = _networkTimer.TimeBetweenTick / (1f / Time.deltaTime);
            rb.AddForce(new Vector3(_xVelocityDifference, 0f, _zVelocityDifference), ForceMode.VelocityChange); 
            
        }
        
        private void Look(Vector3 inputLookVector)
        {
            _simulationYaw += inputLookVector.x * sensitivity * Time.fixedDeltaTime;

            Quaternion yawRotation = Quaternion.Euler(0f, _simulationYaw, 0f);
    
            orientation.rotation = yawRotation;
            transform.rotation = yawRotation;
        }

        

        private void UpdateStamina(bool isRunning)
        {
            bool canRun = isRunning && !_isExhausted;

            if (canRun)
            {
                _stamina -= staminaDrainPerSecond * Time.deltaTime;

                if (_stamina <= 0f)
                {
                    _stamina = 0f;
                    _isExhausted = true;
                }
            }
            else
            {
                _stamina += staminaGainPerSecond * Time.deltaTime;
                _stamina = Mathf.Min(_stamina, staminaMax);

                if (_isExhausted && _stamina >= staminaCooldownThreshold)
                {
                    _isExhausted = false;
                }
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent -= InputReader_OnMoveEvent;
                inputReader.OnRunEvent -= InputReader_OnRunEvent;
                
            }
            
        }
        
    }
}
