using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Monster
{
    public class MonsterSearch : NetworkBehaviour
    {
        public event Action OnSearchStartedAnimation;
        public event Action OnSearchEndedAnimation;
        
        [Header("References")]
        [SerializeField] private Transform monsterTransform;

        [Header("Look Around")]
        [SerializeField] private float minLookDuration = 0.45f;
        [SerializeField] private float maxLookDuration = 0.85f;

        [SerializeField] private float minRotationSpeed = 240f;
        [SerializeField] private float maxRotationSpeed = 420f;

        [Header("Search")]
        [SerializeField] private int minSweeps = 2;
        [SerializeField] private int maxSweeps = 4;

        [SerializeField] private float destinationTolerance = 0.5f;

        private NavMeshAgent _agent;

        private enum SearchPhase
        {
            MovingToLastKnownPosition,
            LookingAround,
            Finished
        }

        private SearchPhase _phase;

        private float _lookTimer;
        private float _lookDuration;
        private float _rotationSpeed;
        private int _rotationDirection;
        private int _sweepCount;
        private int _maxSweeps;

        public bool IsFinished => _phase == SearchPhase.Finished;
        public bool IsLookingAround => _phase == SearchPhase.LookingAround;

        
        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
        }

        
        public void Begin(Vector3 lastKnownPosition, float chaseSpeed)
        {
            _phase = SearchPhase.MovingToLastKnownPosition;

            OnSearchStartedAnimation?.Invoke();
            
            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            _agent.updateRotation = true;
            _agent.SetDestination(lastKnownPosition);

            _lookTimer = 0f;
            _lookDuration = Random.Range(minLookDuration, maxLookDuration);
            _rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);
            _rotationDirection = Random.value < 0.5f ? -1 : 1;

            _sweepCount = 0;
            _maxSweeps = Random.Range(minSweeps, maxSweeps + 1);
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
            if (_agent.pathPending) return;
            if (_agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, destinationTolerance)) return;

            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.updateRotation = false;
            
            OnSearchEndedAnimation?.Invoke();

            _phase = SearchPhase.LookingAround;
        }

        private void UpdateLookAround(float deltaTime)
        {
            monsterTransform.Rotate(0f, _rotationSpeed * _rotationDirection * deltaTime, 0f);

            _lookTimer += deltaTime;

            if (_lookTimer < _lookDuration) return;

            _lookTimer = 0f;

            _lookDuration = Random.Range(minLookDuration, maxLookDuration);
            _rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);
            _rotationDirection = Random.value < 0.5f ? -1 : 1;

            _sweepCount++;

            if (_sweepCount < _maxSweeps) return;

            Finish();
        }

        private void Finish()
        {
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