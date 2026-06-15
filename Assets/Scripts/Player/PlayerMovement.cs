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
        
        [SerializeField] private PlayerState playerState;
        
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Transform orientation;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float blendMovementTime = 8.9f;
        
        [SerializeField] private float minFootstepInterval = 0.25f;
        [SerializeField] private float maxFootstepInterval = 0.6f;
        
        
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

        
        private void InputReader_OnMoveEvent(Vector2 movementInput) => _movementInput = movementInput;
        
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
            
            OnPlayerMovement?.Invoke(_currentVelocity);
        }
        
        private void HandleFootsteps()
        {
            float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

            if (currentSpeed < 0.1f)
            {
                _footstepTimer = 0f;
                return;
            }

            float speedPercent = Mathf.Clamp01(currentSpeed / moveSpeed);
            float currentInterval = Mathf.Lerp(maxFootstepInterval, minFootstepInterval, speedPercent);

            _footstepTimer += Time.fixedDeltaTime;

            if (_footstepTimer >= currentInterval)
            {
                _footstepTimer = 0f;
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
