using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterChase : NetworkBehaviour
    {
        public event Action OnStartedChasingAnimation;
        public event Action OnStoppedChasingAnimation;
        
        [SerializeField] private float chaseSpeed = 8f;
        [SerializeField] private float targetReevaluationInterval = 1f;
        
        public List<Transform> monsterTargets;
        public float DistanceFromTarget => _currentDistanceFromTarget;
        public bool HasTarget => _currentTarget != null;
        
        private NavMeshAgent _agent;
        private List<Transform> _monsterTargets;
        private Transform _currentTarget;
        private float _currentDistanceFromTarget = float.MaxValue;
        private float _reevaluationTimer;
        
        
        public void Initialize(List<Transform> monsterTargetsList, NavMeshAgent agent, MonsterBrain monsterBrain)
        {
            monsterTargets = monsterTargetsList;
            _agent = agent;
            
            if (monsterBrain != null)
            {
                monsterBrain.OnPlayerEnterInVision += MonsterBrain_OnPlayerEnterInVision;
                monsterBrain.OnPlayerExitInVision += MonsterBrain_OnPlayerExitInVision;
            }
        }

        private void MonsterBrain_OnPlayerEnterInVision(Transform player)
        {
            if (!_currentTarget)
            {
                SetTarget(player);
            }
            else
            {
                float distanceFromCurrentTarget = Vector3.Distance(_agent.transform.position, _currentTarget.position);
                float distanceFromNewTarget = Vector3.Distance(_agent.transform.position, player.position);
                
                if (distanceFromNewTarget < distanceFromCurrentTarget)
                {
                    SetTarget(player);
                }
            }
        }
        
        private void MonsterBrain_OnPlayerExitInVision(Transform player)
        {
            if (_currentTarget != player) return;
            
            Transform nextTarget = GetBestAvailableTarget(excludeTarget: player);

            if (nextTarget != null)
            {
                SetTarget(nextTarget);
            }
            else
            {
                ClearTarget();
            }
        }
        
        private Transform GetBestAvailableTarget(Transform excludeTarget = null)
        {
            if (_monsterTargets == null || _monsterTargets.Count == 0) return null;

            return _monsterTargets.Where(t => t != null && t != excludeTarget)
                .OrderBy(t => Vector3.Distance(_agent.transform.position, t.position))
                .FirstOrDefault();
        }

        private void ReevaluateTarget()
        {
            Transform bestTarget = GetBestAvailableTarget();

            if (bestTarget != null && bestTarget != _currentTarget)
                SetTarget(bestTarget);
        }
        
        private void SetTarget(Transform target)
        {
            if (!target || !_agent) return;

            _currentTarget = target;
            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
        }

        private void ClearTarget()
        {
            _currentTarget = null;
            _currentDistanceFromTarget = float.MaxValue;

            if (_agent != null)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        public void StartChase()
        {
            if (_agent == null) return;

            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            OnStartedChasingAnimation?.Invoke();
        }
        
        
        public void ChaseUpdate(float deltaTime)
        {
            if (_agent == null) return;
            
            if (_currentTarget != null && !_currentTarget)
            {
                _currentTarget = null;
                Transform fallback = GetBestAvailableTarget();

                if (fallback != null) SetTarget(fallback);
                else { ClearTarget(); return; }
            }

            if (_currentTarget == null) return;
            
            _reevaluationTimer += deltaTime;
            
            if (_reevaluationTimer >= targetReevaluationInterval)
            {
                _reevaluationTimer = 0f;
                ReevaluateTarget();
            }

            _agent.SetDestination(_currentTarget.position);
        }
        
        public void StopChase()
        {
            if (_agent == null) return;
            
            _agent.isStopped = true;
            OnStoppedChasingAnimation?.Invoke();
        }
        
        public void UpdateDistanceFromTarget()
        {
            if (_currentTarget == null)
            {
                _currentDistanceFromTarget = float.MaxValue;
                return;
            }
    
            _currentDistanceFromTarget = Vector3.Distance(
                _agent.transform.position, 
                _currentTarget.position
            );
        }
        
        public void Uninitialize(List<Transform> monsterTargetsList, NavMeshAgent agent, MonsterBrain monsterBrain)
        {
            monsterTargets = null;
            _agent = null;

            if (monsterBrain != null)
            {
                monsterBrain.OnPlayerEnterInVision -= MonsterBrain_OnPlayerEnterInVision;
                monsterBrain.OnPlayerExitInVision -= MonsterBrain_OnPlayerExitInVision;
            }
        }
    }

}


