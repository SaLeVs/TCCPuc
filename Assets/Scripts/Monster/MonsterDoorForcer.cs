using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterDoorForcer : NetworkBehaviour
    {
        public static Action<Vector3> OnDoorHitSound;

        public event Action OnDoorHitAnimation;
        public event Action OnDoorHitEndedAnimation;

        [Tooltip("How far ahead the monster notices a shut door and stops to deal with it.")]
        [SerializeField] private float detectDistance = 1.3f;

        [Tooltip("Height of the probe off the floor, so it meets the leaf and not the threshold.")]
        [SerializeField] private float probeHeight = 1f;

        [Tooltip("How long the swipe takes before the door gives.")]
        [SerializeField] private float attackSeconds = 0.9f;

        [Tooltip("How fast it squares up with the door.")]
        [SerializeField] private float faceDoorSpeed = 8f;

        [Tooltip("Pause before it will swipe again, so slamming a door in its face cannot make it stutter.")]
        [SerializeField] private float retryCooldown = 0.6f;

        [Tooltip("Layers the door leaves live on.")]
        [SerializeField] private LayerMask doorLayers;

        public bool IsForcingDoor => _door != null;

        private NavMeshAgent _agent;
        private IForceableDoor _door;
        private float _timer;
        private float _cooldown;
        private bool _opened;

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public void Tick(float deltaTime)
        {
            if (!IsServer || _agent == null || !_agent.isOnNavMesh) return;

            if (_door != null)
            {
                UpdateForcing(deltaTime);
                return;
            }

            if (_cooldown > 0f)
            {
                _cooldown -= deltaTime;
                return;
            }

            TryDetectDoor();
        }

        private void TryDetectDoor()
        {
            if (_agent.isStopped || _agent.pathPending) return;

            Vector3 direction = _agent.desiredVelocity;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f) return;

            Vector3 origin = transform.position + Vector3.up * probeHeight;

            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, detectDistance, doorLayers, QueryTriggerInteraction.Ignore)) return;
            
            IForceableDoor door = hit.collider.GetComponentInParent<IForceableDoor>();
            if (door == null || !door.IsClosed) return;

            BeginForcing(door);
        }

        private void BeginForcing(IForceableDoor door)
        {
            _door = door;
            _timer = 0f;
            _opened = false;

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;

            OnDoorHitAnimation?.Invoke();
            PlayHitSoundRpc();
        }

        private void UpdateForcing(float deltaTime)
        {
            FaceDoor(deltaTime);

            _timer += deltaTime;

            if (!_opened)
            {
                if (_timer < attackSeconds) return;

                _door.ForceOpenFrom(transform.position);
                _opened = true;
                return;
            }
            
            if (_door.IsSwinging) return;

            EndForcing();
        }

        private void FaceDoor(float deltaTime)
        {
            Vector3 lookDirection = _door.Position - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude < 0.01f) return;

            Quaternion target = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, faceDoorSpeed * deltaTime);
        }

        private void EndForcing()
        {
            _agent.isStopped = false;

            _door = null;
            _timer = 0f;
            _opened = false;
            _cooldown = retryCooldown;

            OnDoorHitEndedAnimation?.Invoke();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayHitSoundRpc()
        {
            OnDoorHitSound?.Invoke(transform.position);
        }
    }
}
