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
        [SerializeField] private PlayerCamera playerCamera;

        [Tooltip("Optional. Leave empty to use the player's Cinemachine camera, which is the transform that actually carries pitch.")]
        [SerializeField] private Transform rayOriginOverride;

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

        private Transform _rayOrigin;
        private Camera _mainCamera;

        private bool _isDead;
        private bool _isLocked;
        

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                ResolveRayOrigin();

                inputReader.OnInteractEvent += InputReader_OnInteractEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            }
        }
        
        private void ResolveRayOrigin()
        {
            if (rayOriginOverride != null)
            {
                _rayOrigin = rayOriginOverride;
                return;
            }

            if (playerCamera != null && playerCamera.playerCinemachineCamera != null)
            {
                _rayOrigin = playerCamera.playerCinemachineCamera.transform;
                return;
            }

            if (_mainCamera != null)
            {
                _rayOrigin = _mainCamera.transform;
                return;
            }
            
            _rayOrigin = playerView;
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
            _isLocked = isLocked;

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
            if (!_isPlayerHitInteractable || _isDead) return;
            if (_currentInteractable == null) return;

            OnInteractRequested?.Invoke();
            _currentInteractable.Interact(gameObject);
        }

        private void Update()
        {
            if (IsOwner && !_isDead)
            {
                _checkTimer += Time.deltaTime;

                if (_checkTimer >= checkInterval)
                {
                    _checkTimer = 0;
                    _isPlayerHitInteractable = CheckRaycast();
                }
            }
        }

        private bool CheckRaycast()
        {
            if (_currentInteractable != null && (_currentInteractable as MonoBehaviour) == null)
            {
                _currentInteractable = null;
                _currentHighlighted?.Disable();
                _currentHighlighted = null;
            }

            if (_rayOrigin == null) ResolveRayOrigin();

            Transform origin = _rayOrigin != null ? _rayOrigin : playerView;
            _currentRay = new Ray(origin.position, origin.forward);

            if (Physics.Raycast(_currentRay, out RaycastHit hit, interactDistance, layerMask))
            {
                if (hit.collider.TryGetComponent(out IInteractable interactable))
                {
                    bool canInteract = interactable.CanInteract(gameObject);

                    _currentInteractable = canInteract ? interactable : null;

                    ApplyHighlight(hit.collider, canInteract);

                    return canInteract;
                }
            }

            ClearHighlight();
            _currentInteractable = null;
            return false;
        }

        private void ApplyHighlight(Component hitCollider, bool canInteract)
        {
            if (!hitCollider.TryGetComponent(out IHighlighted newHighlight))
            {
                ClearHighlight();
                return;
            }

            if (_currentHighlighted != newHighlight)
            {
                _currentHighlighted?.Disable();
                _currentHighlighted = newHighlight;
            }

            if (canInteract) _currentHighlighted.Enable();
            else _currentHighlighted.EnableBlocked();
        }

        private void ClearHighlight()
        {
            _currentHighlighted?.Disable();
            _currentHighlighted = null;
        }

        private void ResetInteractable()
        {
            ClearHighlight();
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
