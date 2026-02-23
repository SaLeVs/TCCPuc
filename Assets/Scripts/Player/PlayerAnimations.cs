using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private Animator animator;
        
        private readonly int _moveXHash = Animator.StringToHash("_xVelocity");
        private readonly int _moveYHash = Animator.StringToHash("_yVelocity");
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement += PlayerMovement_OnPlayerMove;

            }
        
        }

        private void PlayerMovement_OnPlayerMove(Vector2 currentVelocity)
        {
            animator.SetFloat(_moveXHash, currentVelocity.x);
            animator.SetFloat(_moveYHash, currentVelocity.y);
            
        }

        private void FixedUpdate()
        {
            MoveAnimation();
        }
        
        private void MoveAnimation()
        {
            
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement -= PlayerMovement_OnPlayerMove;
            
            }
        
        }
        
        
    }
}

