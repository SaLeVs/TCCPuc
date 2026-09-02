using System.Collections;
using System.Collections.Generic;
using Enums;
using Interfaces;
using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Objects
{
    [RequireComponent(typeof(NavMeshObstacle))]
    public class Door : NetworkBehaviour, IInteractable
    {
        [Header("References")]
        [SerializeField] private Transform doorFacing;
        [SerializeField] private NavMeshObstacle navMeshObstacle;
        [SerializeField] private List<DoorLeaf> leaves = new List<DoorLeaf>();

        [Header("Settings")]
        [SerializeField] private float openSpeed = 3f;

        [Tooltip("If true, ignore  new interacts when the door is moving.")]
        [SerializeField] private bool blockInteractWhileMoving = false;

        public DoorState CurrentState => _state.Value;
        public bool IsMoving => _rotateRoutine != null;
        

        private readonly NetworkVariable<DoorState> _state = new NetworkVariable<DoorState>(DoorState.Closed);
        private Coroutine _rotateRoutine;

        
        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += Door_OnStateChanged;
            
            ApplyRotationImmediate(_state.Value);
            navMeshObstacle.carving = _state.Value == DoorState.Closed;

            if (IsServer)
            {
                foreach (DoorLeaf leaf in leaves)
                {
                    if (leaf.collisionRelay != null)
                    {
                        leaf.collisionRelay.OnLeafCollisionEnter += Leaf_OnCollisionEnter;
                    }
                }
            }
        }
        

        public bool CanInteract(GameObject interactor)
        {
            return !blockInteractWhileMoving || !IsMoving;
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;

            RequestToggleServerRpc(playerInteractor.transform.position);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void RequestToggleServerRpc(Vector3 playerPosition)
        {
            if (blockInteractWhileMoving && IsMoving) return;

            if (_state.Value == DoorState.Closed)
            {
                Vector3 toPlayer = playerPosition - doorFacing.position;
                toPlayer.y = 0f;
                
                float side = Mathf.Sign(Vector3.Dot(doorFacing.forward, toPlayer.normalized));
                _state.Value = side > 0f ? DoorState.OpenSideB : DoorState.OpenSideA;

                navMeshObstacle.carving = false;
            }
            else
            {
                _state.Value = DoorState.Closed;
                navMeshObstacle.carving = true;
            }
        }
        
        private static float GetStateSign(DoorState state)
        {
            switch (state)
            {
                case DoorState.OpenSideA: return 1f;
                case DoorState.OpenSideB: return -1f;
                default: return 0f;
            }
        }

        private Quaternion GetLeafTargetLocalRotation(DoorLeaf leaf, DoorState state)
        {
            float angle = leaf.openAngle * leaf.mirrorMultiplier * GetStateSign(state);
            return Quaternion.Euler(0f, angle, 0f);
        }

        private void Door_OnStateChanged(DoorState previous, DoorState current)
        {
            if (_rotateRoutine != null)
            {
                StopCoroutine(_rotateRoutine);
            }

            _rotateRoutine = StartCoroutine(RotateDoor(current));
        }

        private IEnumerator RotateDoor(DoorState targetState)
        {
            int count = leaves.Count;
            Quaternion[] startRotations = new Quaternion[count];
            Quaternion[] targetRotations = new Quaternion[count];

            for (int i = 0; i < count; i++)
            {
                startRotations[i] = leaves[i].pivot.localRotation;
                targetRotations[i] = GetLeafTargetLocalRotation(leaves[i], targetState);
            }

            float duration = Mathf.Max(0.01f, 1f / openSpeed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < count; i++)
                {
                    Quaternion next = Quaternion.Slerp(startRotations[i], targetRotations[i], t);
                    ApplyLeafRotation(leaves[i], next);
                }
                
                yield return IsServer ? (object)new WaitForFixedUpdate() : null;
            }

            for (int i = 0; i < count; i++)
            {
                ApplyLeafRotation(leaves[i], targetRotations[i]);
            }

            _rotateRoutine = null;
        }

        private void ApplyLeafRotation(DoorLeaf leaf, Quaternion localRotation)
        {
            if (IsServer && leaf.rigidbodyRef != null)
            {
                leaf.rigidbodyRef.MoveRotation(transform.rotation * localRotation);
            }
            else
            {
                leaf.pivot.localRotation = localRotation;
            }
        }

        private void ApplyRotationImmediate(DoorState state)
        {
            foreach (DoorLeaf leaf in leaves)
            {
                Quaternion target = GetLeafTargetLocalRotation(leaf, state);
                leaf.pivot.localRotation = target;

                if (IsServer && leaf.rigidbodyRef != null)
                {
                    leaf.rigidbodyRef.MoveRotation(transform.rotation * target);
                }
            }
        }

        private void Leaf_OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;

            if (collision.gameObject.TryGetComponent(out PlayerState playerState))
            {
                // TODO: PlayerDown Here
            }
        }
        
        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= Door_OnStateChanged;

            if (IsServer)
            {
                foreach (DoorLeaf leaf in leaves)
                {
                    if (leaf.collisionRelay != null)
                    {
                        leaf.collisionRelay.OnLeafCollisionEnter -= Leaf_OnCollisionEnter;
                    }
                }
            }

            if (_rotateRoutine != null)
            {
                StopCoroutine(_rotateRoutine);
                _rotateRoutine = null;
            }
        }
        
    }
}