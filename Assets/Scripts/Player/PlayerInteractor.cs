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
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform playerView;

        [Header("Settings")]
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private float checkInterval = 0.2f;
        [SerializeField] private LayerMask layerMask;

        private float _checkTimer;
        private Ray _currentRay;
        
        private bool _isPlayerHitInteractable;
        
        private IInteractable _currentInteractable;
        private IHighlighted _currentHighlighted;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnInteractEvent += InputReader_OnInteractEvent;
            }
        }

        
        private void InputReader_OnInteractEvent()
        {
            Interact();
        }

        private void Interact()
        {
            if (_isPlayerHitInteractable)
            {
                OnInteractRequested?.Invoke();
                _currentInteractable.Interact(gameObject);
            }
            
        }

        private void Update()
        {
            if(IsOwner)
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
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnInteractEvent -= InputReader_OnInteractEvent;
            }
        }
    }
}

