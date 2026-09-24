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

        /// <summary>Carries the intended speed so the animator can pick walk or run.</summary>
        public event Action<float> OnStartedMovingAnimation;

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
        private float _moveSpeed;
        private int _searchDirection;

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
            _moveSpeed = chaseSpeed;
            _phase = SearchPhase.MovingToLastKnownPosition;

            _lookTimer = 0f;
            _lookDuration = Random.Range(minLookDuration, maxLookDuration);

            StartMoving();
        }

        /// <summary>
        /// Picks up where it was after the door forcer hands the agent back. The forcer walked it
        /// to a door and reset the path, so the destination has to be asked for again.
        /// </summary>
        public void Resume()
        {
            switch (_phase)
            {
                case SearchPhase.MovingToLastKnownPosition:
                    StartMoving();
                    break;

                case SearchPhase.LookingAround:
                    _agent.isStopped = true;
                    OnSearchStartedAnimation?.Invoke(_searchDirection);
                    break;
            }
        }

        /// <summary>
        /// The last known position cannot be reached, or the agent has stopped getting closer.
        /// Sweep from here instead of running at it forever.
        /// </summary>
        public void AbandonMove()
        {
            if (_phase != SearchPhase.MovingToLastKnownPosition) return;

            StartLookingAround();
        }

        private void StartMoving()
        {
            _agent.isStopped = false;
            _agent.speed = _moveSpeed;
            _agent.updateRotation = false;

            _agent.SetDestination(_lastKnownPosition);

            // Without this the run to the last known position kept whatever clip the previous
            // state left playing — usually an idle, so it slid there on frozen feet.
            OnStartedMovingAnimation?.Invoke(_moveSpeed);
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

            StartLookingAround();
        }

        private void StartLookingAround()
        {
            _agent.isStopped = true;
            _agent.ResetPath();

            _phase = SearchPhase.LookingAround;

            _searchDirection = Random.Range(1, 3);
            OnSearchStartedAnimation?.Invoke(_searchDirection);
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