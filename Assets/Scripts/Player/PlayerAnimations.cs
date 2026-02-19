using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private InputReader inputReader;
    
        private readonly int _moveXHash = Animator.StringToHash("_xVelocity");
        private readonly int _moveYHash = Animator.StringToHash("_yVelocity");
        private readonly int _isRunningHash = Animator.StringToHash("_isRunning");
        
        private const float SMOOTH_TIME = 0.01f;
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent += InputReader_OnMoveEvent;
                inputReader.OnRunEvent += InputReader_OnRunEvent;
            
            }
        
        }

    
        private void InputReader_OnMoveEvent(Vector2 movementInput)
        {
            animator.SetFloat(_moveXHash, movementInput.x, SMOOTH_TIME, Time.deltaTime);
            animator.SetFloat(_moveYHash, movementInput.y, SMOOTH_TIME, Time.deltaTime);
            
        }
    
        private void InputReader_OnRunEvent(bool isRunning)
        {
            animator.SetBool(_isRunningHash, isRunning);
            
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

