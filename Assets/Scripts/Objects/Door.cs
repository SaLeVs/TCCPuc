using System.Collections;
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
        [SerializeField] private Transform doorPivot;
        [SerializeField] private Rigidbody doorRigidbody;
        [SerializeField] private NavMeshObstacle navMeshObstacle;

        [Header("Settings")]
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float openSpeed = 3f;

        private readonly NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<float> _openSign = new NetworkVariable<float>(1f);

        private Coroutine _rotateRoutine;

        public override void OnNetworkSpawn()
        {
            _isOpen.OnValueChanged += OnIsOpenChanged;

            navMeshObstacle.carving = !_isOpen.Value;
            Quaternion snapRotation = Quaternion.Euler(0f, _isOpen.Value ? openAngle * _openSign.Value : 0f, 0f);
            doorPivot.localRotation = snapRotation;

            if (IsServer)
            {
                doorRigidbody.MoveRotation(transform.rotation * snapRotation);
            }
        }

        public override void OnNetworkDespawn()
        {
            _isOpen.OnValueChanged -= OnIsOpenChanged;

            if (_rotateRoutine != null)
            {
                StopCoroutine(_rotateRoutine);
                _rotateRoutine = null;
            }
        }

        public bool CanInteract(GameObject interactor) => true;

        public bool Interact(GameObject playerInteractor)
        {
            Debug.Log($"Door interacted by {playerInteractor.name} at position {playerInteractor.transform.position}");
            RequestToggleServerRpc(playerInteractor.transform.position);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void RequestToggleServerRpc(Vector3 playerPosition)
        {
            if (!_isOpen.Value)
            {
                Vector3 toPlayer = playerPosition - doorPivot.position;
                toPlayer.y = 0f;
                float side = Mathf.Sign(Vector3.Dot(doorPivot.forward, toPlayer.normalized));
                
                _openSign.Value = -side;

                navMeshObstacle.carving = false;
            }
            else
            {
                navMeshObstacle.carving = true;
            }

            _isOpen.Value = !_isOpen.Value;
        }

        private void OnIsOpenChanged(bool previous, bool current)
        {
            if (_rotateRoutine != null)
            {
                StopCoroutine(_rotateRoutine);
            }

            _rotateRoutine = StartCoroutine(RotateDoor(current));
        }

        private IEnumerator RotateDoor(bool open)
        {
            Quaternion startRot = doorPivot.localRotation;
            Quaternion targetRot = Quaternion.Euler(0f, open ? openAngle * _openSign.Value : 0f, 0f);
            float duration = Mathf.Max(0.01f, 1f / openSpeed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Quaternion next = Quaternion.Slerp(startRot, targetRot, elapsed / duration);

                if (IsServer)
                {
                    doorRigidbody.MoveRotation(transform.rotation * next);
                    yield return new WaitForFixedUpdate();
                }
                else
                {
                    doorPivot.localRotation = next;
                    yield return null;
                }
            }

            if (IsServer)
            {
                doorRigidbody.MoveRotation(transform.rotation * targetRot);
            }
            else
            {
                doorPivot.localRotation = targetRot;
            }

            _rotateRoutine = null;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;

            if (collision.gameObject.TryGetComponent(out PlayerState playerState))
            {
                // TODO: Player ragdoll
            }
        }
    }
}