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
        public event Action OnInteract;
        public event Action<int> OnHoldItem;
        
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerRun playerRun;
        [SerializeField] private PlayerCrouch playerCrouch;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerInteractor playerInteractor;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement += PlayerMovement_OnPlayerMovement;
                playerRun.OnRunEvent += PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent += PlayerCrouch_OnCrouchEvent;
                
                playerInteractor.OnInteractRequested += PlayerInteractor_OnInteractRequested;
                playerInventory.OnSelectedSlotChanged += PlayerInventory_OnSelectedSlotChanged;
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
        
        private void PlayerInteractor_OnInteractRequested()
        {
            OnInteract?.Invoke();
        }
        
        private void PlayerInventory_OnSelectedSlotChanged(int slot)
        {
            OnHoldItem?.Invoke(slot);
        }
        
        
        
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement -= PlayerMovement_OnPlayerMovement;
                playerRun.OnRunEvent -= PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent -= PlayerCrouch_OnCrouchEvent;
                
                playerInteractor.OnInteractRequested -= PlayerInteractor_OnInteractRequested;
                playerInventory.OnSelectedSlotChanged -= PlayerInventory_OnSelectedSlotChanged;
            }
            
        }
    }
}