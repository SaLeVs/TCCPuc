using System;
using Inputs;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerInteractor : NetworkBehaviour
    {
        public event Action OnInteractRequested;
        
        [Header("References")]
        [SerializeField] private PlayerState playerState;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform playerView;

        [Header("Settings")]
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private float checkInterval = 0.2f;
        [SerializeField] private LayerMask layerMask;

        public IInteractable CurrentInteractable => _currentInteractable;
        
        private float _checkTimer;
        private Ray _currentRay;
        
        private bool _isPlayerHitInteractable;
        
        private IInteractable _currentInteractable;
        private IHighlighted _currentHighlighted;

        private bool _isDead;
        private bool _isLocked;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnInteractEvent += InputReader_OnInteractEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            }
        }
        
        
        private void PlayerState_OnPlayerDead(bool isDead)
        {
            _isDead = isDead;
    
            if (_isDead)
            {
                ResetInteractable();
            }
        }

        private void PlayerState_OnPlayerLocked(bool isLocked)
        {
            _isLocked =  isLocked;

            if (_isLocked)
            {
                ResetInteractable();
            }
        }

        private void InputReader_OnInteractEvent()
        {
            Interact();
        }

        private void Interact()
        {
            if (_isPlayerHitInteractable && !_isDead)
            {
                OnInteractRequested?.Invoke();
                _currentInteractable.Interact(gameObject);
            }
            
        }

        private void Update()
        {
            if (IsOwner && !_isDead)
            {
                _checkTimer += Time.deltaTime;

                if(_checkTimer >= checkInterval)
                {
                    _checkTimer = 0;
                    _isPlayerHitInteractable = CheckRaycast();
                } 
            }
            
        }

        private bool CheckRaycast()
        {
            _currentRay = new Ray(playerView.position, playerView.forward);

            if (Physics.Raycast(_currentRay, out RaycastHit hit, interactDistance, layerMask))
            {
                if (hit.collider.TryGetComponent(out IInteractable interactable))
                {
                    _currentInteractable = interactable;

                    if (hit.collider.TryGetComponent(out IHighlighted newHighlight))
                    {
                        if (_currentHighlighted != newHighlight)
                        {
                            _currentHighlighted?.Disable();
                            _currentHighlighted = newHighlight;
                            _currentHighlighted.Enable();   
                        }
                    }

                    return true;
                }
            }
            
            _currentHighlighted?.Disable();
            _currentHighlighted = null;
            _currentInteractable = null;
            return false;
        }

        private void ResetInteractable()
        {
            _currentHighlighted?.Disable();
            _currentHighlighted = null;
            _currentInteractable = null;
            _isPlayerHitInteractable = false;
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnInteractEvent -= InputReader_OnInteractEvent;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
            }
        }
        
    }
}

