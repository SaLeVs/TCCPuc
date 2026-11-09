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
        [Tooltip("Reach for anything that does not ask for its own (see IInteractionRange).")]
        [SerializeField] private float interactDistance = 3f;

        [Tooltip("The furthest any object may ask to be reached from. The ray is cast this far and each " +
                 "hit is then held to its own reach, so raising it lets doors be caught on the run " +
                 "without letting items be grabbed from further away.")]
        [SerializeField] private float maxInteractDistance = 3f;

        [SerializeField] private float checkInterval = 0.2f;
        [SerializeField] private LayerMask layerMask;

        [Tooltip("What hides something reached beyond the default distance, or an open doorway, from the " +
                 "crosshair. Empty means Default + Ground + Scenary + Walls + MainStage.")]
        [SerializeField] private LayerMask occlusionMask;

        [Tooltip("Audience props, which light their outline when the crosshair rests on them even though " +
                 "there is nothing to do with them. Empty means Recordable.")]
        [SerializeField] private LayerMask recordableMask;

        [Tooltip("How far the crosshair lights an audience prop's outline. These are filmed rather than " +
                 "picked up, so this follows the camera's reach instead of the hand's.")]
        [SerializeField, Min(0f)] private float recordableHighlightDistance = 10f;

        public IInteractable CurrentInteractable => _currentInteractable;

        /// <summary>
        /// What the crosshair rests on, even when it refuses the player. The HUD needs the refused
        /// case too: <see cref="CurrentInteractable"/> goes null there, which would read as "nothing
        /// here" instead of "this is not yours".
        /// </summary>
        public IInteractable HoveredInteractable => _hoveredInteractable;

        public bool IsHoveredBlocked => _hoveredInteractable != null && _currentInteractable == null;

        private float _checkTimer;
        private Ray _currentRay;

        private bool _isPlayerHitInteractable;

        private IInteractable _currentInteractable;
        private IInteractable _hoveredInteractable;
        private IHighlighted _currentHighlighted;

        private Transform _rayOrigin;
        private Camera _mainCamera;

        private bool _isDead;
        private bool _isLocked;

        // Items share the layers doors are found on, so room for a pile of them and the door behind it.
        private readonly Collider[] _doorwayCandidates = new Collider[32];

        private readonly RaycastHit[] _recordableHits = new RaycastHit[8];


        private void Awake()
        {
            _mainCamera = Camera.main;

            if (occlusionMask.value == 0)
            {
                occlusionMask = LayerMask.GetMask("Default", "Ground", "Scenary", "Walls", "MainStage");
            }

            if (recordableMask.value == 0)
            {
                recordableMask = LayerMask.GetMask("Recordable");
            }
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
            if (!IsOwner || _isDead) return;

            // Look again now rather than trust the timed check, which can be a fifth of a second
            // old: at a run that is more than a metre, and the door the crosshair is on now was not
            // under it then.
            _checkTimer = 0f;
            _isPlayerHitInteractable = CheckRaycast();

            if (!_isPlayerHitInteractable) return;
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
                _hoveredInteractable = null;
                _currentHighlighted?.Disable();
                _currentHighlighted = null;
            }

            if (_rayOrigin == null) ResolveRayOrigin();

            Transform origin = _rayOrigin != null ? _rayOrigin : playerView;
            _currentRay = new Ray(origin.position, origin.forward);

            if (Physics.Raycast(_currentRay, out RaycastHit hit, MaxReach, layerMask)
                && hit.collider.TryGetComponent(out IInteractable interactable)
                && IsWithinReach(interactable, hit.distance, hit.collider))
            {
                return Target(interactable, hit.collider);
            }

            // Nothing under the crosshair itself: an open doorway in front of it still counts as its door.
            if (TryFindDoorway(out IDoorwayInteractable doorway, out Collider leaf))
            {
                return Target(doorway, leaf);
            }

            _currentInteractable = null;
            _hoveredInteractable = null;

            // Nothing to use, but an audience prop under the crosshair still lights its outline, so
            // the player can tell what is worth filming. It never becomes the interactable.
            if (!TryHighlightRecordable()) ClearHighlight();

            return false;
        }

        /// <summary>
        /// The nearest audience prop the crosshair rests on, through trigger volumes but not walls:
        /// the recordable layer is cast on its own, so a prop in the next room would otherwise light
        /// up through the partition.
        /// </summary>
        private bool TryHighlightRecordable()
        {
            int count = Physics.RaycastNonAlloc(_currentRay, _recordableHits, recordableHighlightDistance,
                recordableMask, QueryTriggerInteraction.Collide);

            Collider nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _recordableHits[i];
                if (hit.distance >= nearestDistance) continue;
                if (!hit.collider.TryGetComponent(out IHighlighted _)) continue;

                nearest = hit.collider;
                nearestDistance = hit.distance;
            }

            if (nearest == null || !IsInPlainSight(nearestDistance, nearest)) return false;

            ApplyHighlight(nearest, true);
            return true;
        }

        private bool Target(IInteractable interactable, Collider part)
        {
            bool canInteract = interactable.CanInteract(gameObject);

            _currentInteractable = canInteract ? interactable : null;
            _hoveredInteractable = interactable;

            ApplyHighlight(part, canInteract);

            return canInteract;
        }

        private float MaxReach => Mathf.Max(interactDistance, maxInteractDistance);

        private float ReachOf(IInteractable interactable)
        {
            return interactable is IInteractionRange range
                ? Mathf.Min(range.InteractionRange, MaxReach)
                : interactDistance;
        }

        /// <summary>
        /// Within the default distance the ray behaves as it always has. Past it, only something
        /// that asked for the extra reach counts, and only if no wall stands in front of it: the
        /// ray ignores everything outside <see cref="layerMask"/>, which a metre further on is
        /// enough to reach a door on the other side of a partition.
        /// </summary>
        private bool IsWithinReach(IInteractable interactable, float distance, Collider part)
        {
            if (distance > ReachOf(interactable)) return false;

            return distance <= interactDistance || IsInPlainSight(distance, part);
        }

        private bool IsInPlainSight(float distance, Collider part)
        {
            if (!Physics.Raycast(_currentRay, out RaycastHit blocker, distance, occlusionMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return blocker.collider == part;
        }

        /// <summary>
        /// The nearest doorway the crosshair passes through, open or shut. Doors are found by
        /// their leaves, which stay within a leaf's width of the opening however far they swing.
        /// </summary>
        private bool TryFindDoorway(out IDoorwayInteractable doorway, out Collider leaf)
        {
            doorway = null;
            leaf = null;

            int count = Physics.OverlapSphereNonAlloc(_currentRay.origin, MaxReach, _doorwayCandidates, layerMask,
                QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                IDoorwayInteractable candidate = _doorwayCandidates[i].GetComponentInParent<IDoorwayInteractable>();
                if (candidate == null) continue;

                if (!candidate.TryRaycastDoorway(_currentRay, ReachOf(candidate), out float distance, out Collider candidateLeaf)) continue;
                if (distance >= nearest) continue;
                if (!IsInPlainSight(distance, candidateLeaf)) continue;

                nearest = distance;
                doorway = candidate;
                leaf = candidateLeaf;
            }

            return doorway != null;
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
            _hoveredInteractable = null;
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
