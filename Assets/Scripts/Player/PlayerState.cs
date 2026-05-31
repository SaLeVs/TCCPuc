using System;
using Interfaces;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerState : NetworkBehaviour, IInputLockable
    {
        public event Action<Vector2> OnPlayerMovement;
        public event Action<bool> OnRunEvent;
        public event Action<bool> OnCrouchEvent;
        public event Action OnInteract;
        public event Action<int> OnHoldItem;
        public event Action<bool> OnPlayerDead;
        public event Action<bool> OnPlayerLocked;
        
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerRun playerRun;
        [SerializeField] private PlayerCrouch playerCrouch;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private PlayerDead playerDead;
        [SerializeField] private PlayerCamera playerCamera;

        public bool IsDead => playerDead.IsDead;
        public CinemachineCamera PlayerCinemachineCamera => playerCamera.playerCinemachineCamera;
        
        private bool _isInputLocked;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement += PlayerMovement_OnPlayerMovement;
                playerRun.OnRunEvent += PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent += PlayerCrouch_OnCrouchEvent;
                
                playerInteractor.OnInteractRequested += PlayerInteractor_OnInteractRequested;
                playerInventory.OnSelectedSlotChanged += PlayerInventory_OnSelectedSlotChanged;
                
                playerDead.OnDeathEvent += PlayerDead_OnDeathEvent;
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
        
        private void PlayerDead_OnDeathEvent(bool isDead)
        {
            OnPlayerDead?.Invoke(isDead);
        }

        public void SetInputLocked(bool locked)
        {
            if (!IsOwner) return;
    
            _isInputLocked = locked;
            OnPlayerLocked?.Invoke(locked);
        }
        
        public void SetSpectatorMode(bool isSpectating) => playerCamera.SetSpectatorMode(isSpectating);
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                playerMovement.OnPlayerMovement -= PlayerMovement_OnPlayerMovement;
                playerRun.OnRunEvent -= PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent -= PlayerCrouch_OnCrouchEvent;
                
                playerInteractor.OnInteractRequested -= PlayerInteractor_OnInteractRequested;
                playerInventory.OnSelectedSlotChanged -= PlayerInventory_OnSelectedSlotChanged;
                
                playerDead.OnDeathEvent -= PlayerDead_OnDeathEvent;
            }
        }
        
    }
}