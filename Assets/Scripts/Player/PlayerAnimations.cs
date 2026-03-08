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

            // animator.SetFloat(_moveXHash, playerState.speed.x, animationSmoothing, Time.deltaTime);
            // animator.SetFloat(_moveYHash, playerState.speed.y,animationSmoothing, Time.deltaTime );
        }
        
        private void PlayerState_OnPlayerMovement(Vector2 playerMovement)
        {
            
        }
        
        private void PlayerState_OnRunEvent(bool isRunning)
        {
            
        }
        
        private void PlayerState_OnCrouchEvent(bool isCrouching)
        {
            
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

