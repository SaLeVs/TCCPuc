using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private InputReader inputReader;
    
        private readonly int _moveXHash = Animator.StringToHash("xVelocity");
        private readonly int _moveYHash = Animator.StringToHash("xVelocity");
        private readonly int _isRunningHash = Animator.StringToHash("_isRunning");
    
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
        
        }
    
        private void InputReader_OnRunEvent(bool isRunning)
        {
        
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

