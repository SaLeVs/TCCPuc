using System;
using UnityEngine;
using Inputs;
using Interfaces;
using Unity.Netcode;


namespace Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        public event Action<Vector2> OnPlayerMovement;
        public static Action<Vector3> OnFootstepSound;
        public event Action<Vector2> OnPlayerMovementInput;
        
        [SerializeField] private PlayerState playerState;
        
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Transform orientation;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float blendMovementTime = 8.9f;
        
        [SerializeField] private float footstepDistance = 1.8f;
        [SerializeField] private float minMoveSpeedToStep = 0.1f;
        
        
        private float _targetSpeed;
        
        private Vector2 _movementInput;
        private Vector3 _movementDirection;
        
        private Vector2 _currentVelocity;
        private float _xVelocityDifference;
        private float _zVelocityDifference;
        
        private ISpeedModifier[] _speedModifiers;
        
        private bool _isDead;
        private bool _isLocked;
        
        private float _footstepTimer;
        private float _distanceSinceLastFootstep;
        private Vector3 _lastFootstepPosition;        
        
        
        private void Awake()
        {
            SetupSpeedModifiers();
        }

        private void SetupSpeedModifiers()
        {
            _speedModifiers = GetComponents<ISpeedModifier>();
        }
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent += InputReader_OnMoveEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            }
        }

        
        private void InputReader_OnMoveEvent(Vector2 movementInput)
        {
            _movementInput = movementInput;
            OnPlayerMovementInput?.Invoke(movementInput);
        } 
        
        private void PlayerState_OnPlayerDead(bool isDead)
        {
            _isDead = isDead;
            
            if (isDead)
            {
                _movementInput = Vector2.zero;
            }
        }
        
        private void PlayerState_OnPlayerLocked(bool isLocked)
        {
            _isLocked = isLocked;
            
            if(_isLocked)
            {
                _movementInput = Vector2.zero;
                StopMovement();
            }
        }
        
        private void FixedUpdate()
        {
            if (IsOwner && !_isDead && !_isLocked)
            {
                Move();
            }
            
            HandleFootsteps();
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
            
            float normalizedSpeed = _targetSpeed > 0.001f ? _targetSpeed : 1f;
            OnPlayerMovement?.Invoke(_currentVelocity / normalizedSpeed);
        }
        
        private void StopMovement()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            _currentVelocity = Vector2.zero;
            _movementInput = Vector2.zero;
            
            _xVelocityDifference = 0f;
            _zVelocityDifference = 0f;
        }
        
        private void HandleFootsteps()
        {
            Vector3 currentPosition = transform.position;
            Vector3 flatDelta = currentPosition - _lastFootstepPosition;
            flatDelta.y = 0f;

            float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

            if (currentSpeed < minMoveSpeedToStep)
            {
                _distanceSinceLastFootstep = 0f;
                _lastFootstepPosition = currentPosition;
                return;
            }

            _distanceSinceLastFootstep += flatDelta.magnitude;
            _lastFootstepPosition = currentPosition;

            if (_distanceSinceLastFootstep >= footstepDistance)
            {
                _distanceSinceLastFootstep -= footstepDistance;
                OnFootstepSound?.Invoke(transform.position);
            }
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
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
            }
        }
        
    }
}
