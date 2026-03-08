using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private Animator animator;
        [SerializeField] private float animationSmoothing = 0.1f;
        
        private readonly int _moveXHash = Animator.StringToHash("_xVelocity");
        private readonly int _moveYHash = Animator.StringToHash("_yVelocity");
        private readonly int _isRunningHash = Animator.StringToHash("_isRunning");
        private readonly int _isCrouchingHash = Animator.StringToHash("_isCrouching");
        private readonly int _speedHash = Animator.StringToHash("_speed");
        
        private float _targetMoveX;
        private float _targetMoveY;
        private float _speed;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerMovement += PlayerState_OnPlayerMovement;
                playerState.OnRunEvent += PlayerState_OnRunEvent;
                playerState.OnCrouchEvent += PlayerState_OnCrouchEvent;
            }
            
        }
        
        private void Update()
        {
            if (IsOwner)
            {
                animator.SetFloat(_moveXHash, _targetMoveX, animationSmoothing, Time.deltaTime);
                animator.SetFloat(_moveYHash, _targetMoveY, animationSmoothing, Time.deltaTime);
                
                _speed = new Vector2(_targetMoveX, _targetMoveY).magnitude;
                animator.SetFloat(_speedHash, _speed, animationSmoothing, Time.deltaTime);
            }
            
        }
        
        private void PlayerState_OnPlayerMovement(Vector2 playerMovement)
        {
            Vector3 worldVel = new Vector3(playerMovement.x, 0f, playerMovement.y);
            Vector3 localVel = transform.InverseTransformDirection(worldVel);
            
            _targetMoveX = localVel.x;
            _targetMoveY = localVel.z;
        }
        
        private void PlayerState_OnRunEvent(bool isRunning)
        {
            animator.SetBool(_isRunningHash, isRunning);
        }
        
        private void PlayerState_OnCrouchEvent(bool isCrouching)
        {
            animator.SetBool(_isCrouchingHash, isCrouching);
        }

        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerMovement -= PlayerState_OnPlayerMovement;
                playerState.OnRunEvent -= PlayerState_OnRunEvent;
                playerState.OnCrouchEvent -= PlayerState_OnCrouchEvent;
            }
            
        }
    }
}

