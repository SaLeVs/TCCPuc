using System;
using System.Collections.Generic;
using Monster;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Monster
{ 
    public class MonsterWander : NetworkBehaviour
    {
        public event Action OnStartedMovingAnimation;
        public event Action OnStoppedMovingAnimation;
        public static Action<Vector3> OnMonsterFootstepSound;
        
        [SerializeField] private float walkSpeed;
        [SerializeField] private float wanderRadius;
        [SerializeField] private float minWanderIntervalForEachPoint;
        [SerializeField] private float maxWanderIntervalForEachPoint;
        [SerializeField] private float minTimeInSector;
        [SerializeField] private float maxTimeInSector;
        [SerializeField] private float waypointReachedDistance;
        [SerializeField] private PatrolSector[] allSectors;
        
        [SerializeField] private float minFootstepInterval = 0.3f;
        [SerializeField] private float maxFootstepInterval = 0.7f;

        private float _footstepTimer;
        
        private NavMeshAgent _agent;
        private PatrolSector _currentSector;

        private float _sectorTimer;
        private float _sectorDuration;
        private float _wanderTimer;
        private float _currentWanderInterval;

        private bool _waitingAtPoint;
        
        
        public void Initialize(NavMeshAgent monsterAgent)
        {
            _agent = monsterAgent;
        }
        
        public void StartWander()
        {
            _agent.isStopped = false;
            _agent.speed = walkSpeed;
            OnStartedMovingAnimation?.Invoke();

            MigrateToNewSector();
        }
        
        private void MigrateToNewSector()
        {
            _currentSector = GetMostRelevantSector();
            
            _sectorDuration = Random.Range(minTimeInSector, maxTimeInSector);
            _currentWanderInterval = Random.Range(minWanderIntervalForEachPoint, maxWanderIntervalForEachPoint);
            
            _wanderTimer = _currentWanderInterval;
            _sectorTimer = 0f;
        }
        
        private PatrolSector GetMostRelevantSector()
        {
            Vector3 playersCenter = GetPlayersCenter();

            PatrolSector mostRelevantSector = null;
            float bestDistance = float.MaxValue;

            foreach (PatrolSector sector in allSectors)
            {
                if (sector == _currentSector && allSectors.Length > 1) continue;

                float distance = Vector3.Distance(sector.Position, playersCenter);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    mostRelevantSector = sector;
                }
            }

            return mostRelevantSector ?? allSectors[Random.Range(0, allSectors.Length)];
        }
        
        /// <summary>
        /// Get the position in center of all players, make monster goes to the closest position off all players
        /// </summary>
        /// <returns></returns>
        private Vector3 GetPlayersCenter()
        {
            IReadOnlyList<NetworkClient> allClients = NetworkManager.Singleton.ConnectedClientsList;

            if (allClients == null || allClients.Count == 0)
            {
                return transform.position;
            }

            Vector3 playersTotalPositions = Vector3.zero;
            int playersCount = 0;

            foreach (NetworkClient client in allClients)
            {
                if (client.PlayerObject != null)
                {
                    playersTotalPositions += client.PlayerObject.transform.position;
                    playersCount++;
                }
            }
            
            return playersCount > 0 ? playersTotalPositions / playersCount : transform.position;
        }
        
        public void UpdateWander(float deltaTime)
        {
            _sectorTimer += deltaTime;

            if (_sectorTimer >= _sectorDuration)
            {
                MigrateToNewSector();
            }
            
            if (!_waitingAtPoint && ReachedDestination())
            {
                _waitingAtPoint = true;
                _wanderTimer = 0f;
                _agent.isStopped = true;
                
                OnStoppedMovingAnimation?.Invoke();
            }

            if (_waitingAtPoint)
            {
                _wanderTimer += deltaTime;

                if (_wanderTimer >= _currentWanderInterval)
                {
                    _waitingAtPoint = false;
                    _wanderTimer = 0f;
                    _currentWanderInterval = Random.Range(minWanderIntervalForEachPoint, maxWanderIntervalForEachPoint);
                    _agent.isStopped = false;
                    _agent.SetDestination(_currentSector.GetRandomPointInSector());
                    
                    OnStartedMovingAnimation?.Invoke();
                }
            }
            
            HandleFootsteps(deltaTime);
        }
        
        private void HandleFootsteps(float deltaTime)
        {
            if (_agent == null || _agent.isStopped)
            {
                _footstepTimer = 0f;
                return;
            }

            float currentSpeed = _agent.velocity.magnitude;

            if (currentSpeed < 0.1f)
            {
                _footstepTimer = 0f;
                return;
            }

            float speedPercent = Mathf.Clamp01(currentSpeed / walkSpeed);
            float currentInterval = Mathf.Lerp(maxFootstepInterval, minFootstepInterval, speedPercent);

            _footstepTimer += deltaTime;

            if (_footstepTimer >= currentInterval)
            {
                _footstepTimer = 0f;

                OnMonsterFootstepSound?.Invoke(transform.position);
            }
        }
        
        public void StopWander()
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _sectorTimer = 0f;
            _wanderTimer = 0f;
        }
        
        private bool ReachedDestination()
        {
            return !_agent.pathPending && _agent.remainingDistance <= waypointReachedDistance;
        } 
    }
}
