using System;
using System.Collections;
using Interfaces;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerState : NetworkBehaviour, IInputLockable
    {
        public event Action<Vector2> OnPlayerMovement;
        public event Action<Vector2> OnPlayerMovementInput;
        public event Action<bool> OnRunEvent;
        public event Action<bool> OnCrouchEvent;
        public event Action OnInteract;
        public event Action<int> OnHoldItem;
        public event Action<bool> OnPlayerDead;
        public event Action<bool> OnPlayerLocked;
        public event Action OnPlayerWon;
        public event Action OnVictoryTriggered;
        public event Action OnGameOverTriggered;
        
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerRun playerRun;
        [SerializeField] private PlayerCrouch playerCrouch;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private PlayerDead playerDead;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private PlayerCameraOffset playerCameraOffset;

        
        public bool IsDead => playerDead.IsDead;
        public CinemachineCamera PlayerCinemachineCamera => playerCamera.playerCinemachineCamera;
        public bool HasEscapedServerSide { get; set; }
        public bool HasWon { get; private set; }
        
        private bool _isInputLocked;
        private Vector2 _movementInput;
        
        
        public override void OnNetworkSpawn()
        {
            Physics.SyncTransforms();
            playerDead.OnDeathEvent += PlayerDead_OnDeathEvent;

            if (IsOwner)
            {
                playerMovement.OnPlayerMovement += PlayerMovement_OnPlayerMovement;
                playerMovement.OnPlayerMovementInput += PlayerMovement_OnPlayerMovementInput;

                playerRun.OnRunEvent += PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent += PlayerCrouch_OnCrouchEvent;
                
                playerInteractor.OnInteractRequested += PlayerInteractor_OnInteractRequested;
                playerInventory.OnSelectedSlotChanged += PlayerInventory_OnSelectedSlotChanged;
                
               
                playerDead.OnRagdollSpawned += PlayerDead_OnRagdollSpawned;
            }
        }
        
        
        private void PlayerMovement_OnPlayerMovement(Vector2 playerVelocity)
        {
            OnPlayerMovement?.Invoke(playerVelocity);
        }
        
        private void PlayerMovement_OnPlayerMovementInput(Vector2 playerInput)
        {
            OnPlayerMovementInput?.Invoke(playerInput);
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

            if (isDead && IsOwner) 
            {
                NotifyDeathServerRpc();
            }
        }
        
        private void PlayerDead_OnRagdollSpawned(Transform ragdollBone)
        {
            playerCameraOffset.SetDeathCameraBone(ragdollBone);
        }


        public void SetInputLocked(bool locked)
        {
            if (!IsOwner) return;
    
            _isInputLocked = locked;
            OnPlayerLocked?.Invoke(locked);
        }
        
        public void SetSpectatorMode(bool isSpectating) => playerCamera.SetSpectatorMode(isSpectating);
        
        
        [Rpc(SendTo.ClientsAndHost)]
        public void HidePlayerRpc()
        {
            foreach (var playerRenderer in GetComponentsInChildren<Renderer>())
            {
                playerRenderer.enabled = false;
            }

            foreach (var playerColliders in GetComponentsInChildren<Collider>())
            {
                playerColliders.enabled = false;
            }
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        public void WinRpc()
        {
            HasWon = true;

            if (IsOwner)
            {
                SetSpectatorMode(true);
            }

            OnPlayerWon?.Invoke();
        }
        
        public void TriggerVictoryLocally()
        {
            SetSpectatorMode(false);
            SetInputLocked(true);
            OnVictoryTriggered?.Invoke();
        }
        
        [Rpc(SendTo.Server)]
        private void NotifyDeathServerRpc()
        {
            if (AllPlayersDead())
            {
                BroadcastGameOverRpc();
            }
        }
        
        private bool AllPlayersDead()
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                NetworkObject playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObj == null) continue;
                if (!playerObj.TryGetComponent(out PlayerState ps)) continue;
                if (ps.IsDead || ps.HasEscapedServerSide) continue;
                return false;
            }
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastGameOverRpc()
        {
            NetworkObject localPlayerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayerObj != null && localPlayerObj.TryGetComponent(out PlayerState ps))
            {
                ps.TriggerGameOverLocally();
            }
        }

        public void TriggerGameOverLocally()
        {
            SetInputLocked(true);
            OnGameOverTriggered?.Invoke();
        }
        
        public void SetOcclusionVisible(bool visible) => playerCamera.SetOcclusionRenderersVisible(visible);
        
        public override void OnNetworkDespawn()
        {
            playerDead.OnDeathEvent -= PlayerDead_OnDeathEvent;

            if (IsOwner)
            {
                playerMovement.OnPlayerMovement -= PlayerMovement_OnPlayerMovement;
                playerMovement.OnPlayerMovementInput -= PlayerMovement_OnPlayerMovementInput;
                playerRun.OnRunEvent -= PlayerRun_OnRunEvent;
                playerCrouch.OnCrouchEvent -= PlayerCrouch_OnCrouchEvent;
        
                playerInteractor.OnInteractRequested -= PlayerInteractor_OnInteractRequested;
                playerInventory.OnSelectedSlotChanged -= PlayerInventory_OnSelectedSlotChanged;
        
                playerDead.OnRagdollSpawned -= PlayerDead_OnRagdollSpawned;
            }

        }
        
    }
}