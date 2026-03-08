using System;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerState : NetworkBehaviour
    {
        public event Action<Vector2> OnPlayerMovement;
        public event Action<bool> OnRunEvent;
        public event Action<bool> OnCrouchEvent;
        
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerRun playerRun;
        [SerializeField] private PlayerCrouch playerCrouch;
        
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement += PlayerMovement_OnPlayerMovement;
                playerRun.OnRunEvent += PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent += PlayerCrouch_OnCrouchEvent;
            }
            
        }
        
        
        private void PlayerMovement_OnPlayerMovement(Vector2 playerVelocity)
        {
            OnPlayerMovement?.Invoke(playerVelocity);
        }
        
        private void PlayerRun_OnRunEvent(bool isRunning)
        {
            OnRunEvent?.Invoke(isRunning);
        }
        
        private void PlayerCrouch_OnCrouchEvent(bool isCrouching)
        {
            OnCrouchEvent?.Invoke(isCrouching);
        }
        

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement -= PlayerMovement_OnPlayerMovement;
                playerRun.OnRunEvent -= PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent -= PlayerCrouch_OnCrouchEvent;
            }
            
        }
    }
}