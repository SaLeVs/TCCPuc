using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Monster
{
    public class MonsterSearch : NetworkBehaviour
    {
        public event Action<int> OnSearchStartedAnimation;
        public event Action OnSearchEndedAnimation;

        [Header("Look Around")]
        [SerializeField] private float minLookDuration = 2f;
        [SerializeField] private float maxLookDuration = 3f;

        [Header("Search")]
        [SerializeField] private float destinationTolerance = 0.5f;
        [SerializeField] private float searchLookRotationSpeed = 12f;

        public bool IsFinished => _phase == SearchPhase.Finished;
        public bool IsLookingAround => _phase == SearchPhase.LookingAround;

        private NavMeshAgent _agent;
        private SearchPhase _phase;

        private float _lookTimer;
        private float _lookDuration;
        private Vector3 _lastKnownPosition;

        private enum SearchPhase
        {
            MovingToLastKnownPosition,
            LookingAround,
            Finished
        }

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public void Begin(Vector3 lastKnownPosition, float chaseSpeed)
        {
            _lastKnownPosition = lastKnownPosition;
            _phase = SearchPhase.MovingToLastKnownPosition;

            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            _agent.updateRotation = false;

            _agent.SetDestination(lastKnownPosition);

            _lookTimer = 0f;
            _lookDuration = Random.Range(minLookDuration, maxLookDuration);
        }

        public void Tick(float deltaTime)
        {
            switch (_phase)
            {
                case SearchPhase.MovingToLastKnownPosition:
                    UpdateMove();
                    break;

                case SearchPhase.LookingAround:
                    UpdateLookAround(deltaTime);
                    break;
            }
        }

        private void UpdateMove()
        {
            RotateTowardsMovement();

            if (_agent.pathPending) return;
            if (_agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, destinationTolerance)) return;

            _agent.isStopped = true;
            _agent.ResetPath();

            _phase = SearchPhase.LookingAround;

            int searchDirection = Random.Range(1, 3);
            OnSearchStartedAnimation?.Invoke(searchDirection);
        }

        private void RotateTowardsMovement()
        {
            Vector3 direction = _agent.desiredVelocity;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, searchLookRotationSpeed * Time.deltaTime);
        }

        private void UpdateLookAround(float deltaTime)
        {
            _lookTimer += deltaTime;

            if (_lookTimer < _lookDuration) return;

            Finish();
        }

        private void Finish()
        {
            OnSearchEndedAnimation?.Invoke();

            _agent.updateRotation = true;
            _agent.isStopped = false;

            _phase = SearchPhase.Finished;
        }

        public void Stop()
        {
            _agent.updateRotation = true;
            _agent.isStopped = false;

            _phase = SearchPhase.Finished;
        }
    }
}