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

        [Header("Look Around")]
        [SerializeField] private float minLookDuration = 0.45f;
        [SerializeField] private float maxLookDuration = 0.85f;

        [Header("Search")]
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

        public bool IsFinished => _phase == SearchPhase.Finished;
        public bool IsLookingAround => _phase == SearchPhase.LookingAround;

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
        }

        public void Begin(Vector3 lastKnownPosition, float chaseSpeed)
        {
            _phase = SearchPhase.MovingToLastKnownPosition;

            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            _agent.updateRotation = true;
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
            if (_agent.pathPending) return;
            if (_agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, destinationTolerance)) return;

            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.updateRotation = false;

            _phase = SearchPhase.LookingAround;

            OnSearchStartedAnimation?.Invoke();
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