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
        private readonly int _speedHash = Animator.StringToHash("_speed");
        
        private readonly int _isRunningHash = Animator.StringToHash("_isRunning");
        private readonly int _isCrouchingHash = Animator.StringToHash("_isCrouching");
        
        private readonly int _interactHash = Animator.StringToHash("_interact");
        private readonly int _heldItemHash = Animator.StringToHash("_holdItem");
        
        private readonly int _deadHash = Animator.StringToHash("_dead");
        
        
        private float _targetMoveX;
        private float _targetMoveY;
        private float _speed;
        private bool _isHoldingItem;
        
        private const int UPPER_LAYER_INDEX = 1;
        
        private float _targetUpperLayerWeight;
        private float _currentUpperLayerWeight;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerMovement += PlayerState_OnPlayerMovement;
                playerState.OnRunEvent += PlayerState_OnRunEvent;
                playerState.OnCrouchEvent += PlayerState_OnCrouchEvent;
                
                playerState.OnInteract += PlayerState_OnInteract;
                playerState.OnHoldItem += PlayerState_OnHoldItem;
                
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
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
                
                animator.SetLayerWeight(UPPER_LAYER_INDEX, 1);
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
        
        private void PlayerState_OnInteract()
        {
            animator.SetTrigger(_interactHash);
        }
        
        private void PlayerState_OnHoldItem(int slot)
        {
            _isHoldingItem = slot > 0;

            animator.SetBool(_heldItemHash, _isHoldingItem);
        }
        
        private void PlayerState_OnPlayerDead(bool isDead)
        {
            animator.SetTrigger(_deadHash);
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerState.OnPlayerMovement -= PlayerState_OnPlayerMovement;
                playerState.OnRunEvent -= PlayerState_OnRunEvent;
                playerState.OnCrouchEvent -= PlayerState_OnCrouchEvent;
                
                playerState.OnInteract -= PlayerState_OnInteract;
                playerState.OnHoldItem -= PlayerState_OnHoldItem;
                
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
            }
            
        }
        
    }
}

