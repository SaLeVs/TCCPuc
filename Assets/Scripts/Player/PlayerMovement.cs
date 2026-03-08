using System;
using System.Collections.Generic;
using UnityEngine;
using Inputs;
using Interfaces;
using Unity.Netcode;


namespace Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        public event Action<Vector2> OnPlayerMovement;
        
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Transform orientation;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float blendMovementTime = 8.9f;
        
        private float _targetSpeed;
        
        private Vector2 _movementInput;
        private Vector3 _movementDirection;
        
        private Vector2 _currentVelocity;
        private float _xVelocityDifference;
        private float _zVelocityDifference;
        
        private List<ISpeedModifier> _speedModifiersList;
        private ISpeedModifier[] _speedModifiers;
        
        
        private void Awake()
        {
            SetupSpeedModifiers();
            
        }

        private void SetupSpeedModifiers()
        {
            _speedModifiersList = new List<ISpeedModifier>();
            
            _speedModifiers = GetComponents<ISpeedModifier>();
            
            foreach (var modifier in _speedModifiers)
            {
                _speedModifiersList.Add(modifier);
            }
            
        }

        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent += InputReader_OnMoveEvent;
                
            }
            
        }
        
        private void InputReader_OnMoveEvent(Vector2 movementInput) => _movementInput = movementInput;
        
        
        
        private void FixedUpdate()
        {
            if (IsOwner)
            {
                Move();
            }
            
        }
        
        
        private void Move()
        {
           _targetSpeed = ApplySpeedModifiers(moveSpeed);
            
            Vector3 desiredVelocityWorld = Vector3.zero;
            
            if (_movementInput.sqrMagnitude > 0.0001f)
            {
                desiredVelocityWorld = orientation.forward * _movementInput.y + orientation.right * _movementInput.x;
                desiredVelocityWorld.y = 0f;
                float inputMag = desiredVelocityWorld.magnitude;
                
                if (inputMag > 0.0001f)
                {
                    desiredVelocityWorld = desiredVelocityWorld.normalized * (_targetSpeed * Mathf.Clamp01(_movementInput.magnitude));
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
            
            rb.AddForce(new Vector3(_xVelocityDifference, 0f, _zVelocityDifference), ForceMode.VelocityChange); 
            
            OnPlayerMovement?.Invoke(_currentVelocity);
            
        }
        
        private float ApplySpeedModifiers(float baseSpeed)
        {
            float finalSpeed = baseSpeed;

            foreach (var modifier in _speedModifiers)
            {
                finalSpeed = modifier.ModifySpeed(finalSpeed);
            }

            return finalSpeed;
            
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent -= InputReader_OnMoveEvent;
            }
            
        }
        
    }
}
