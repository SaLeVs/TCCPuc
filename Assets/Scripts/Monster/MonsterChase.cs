using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterChase : NetworkBehaviour
    {
        [SerializeField] private float chaseSpeed = 8f;
        
        public List<Transform> monsterTargets;
        public float DistanceFromTarget => _currentDistanceFromTarget;
        
        private NavMeshAgent _agent;
        private Transform _currentTarget;
        private float _currentDistanceFromTarget;
        
        
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
            if (_currentTarget == player)
            {
                ClearTarget();
                
                if (monsterTargets.Count > 0)
                {
                    List<Transform> currentListOfPlayers = monsterTargets.Where(monsterTarget => monsterTarget).ToList();
                    
                    if (currentListOfPlayers.Count > 0)
                    {
                        Transform nextTargetInList =
                            currentListOfPlayers.OrderBy(playerTarget => Vector3.Distance(_agent.transform.position, playerTarget.position)).First();
                        
                        SetTarget(nextTargetInList);
                    }
                }
            }
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
            _currentDistanceFromTarget = 0;
            
            if (_agent)
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
        }
        
        public void ChaseUpdate()
        {
            if (!_agent) return;
            if (!_currentTarget) return;
            
            _agent.SetDestination(_currentTarget.position);
            _currentDistanceFromTarget =
                Vector3.Distance(_agent.transform.position, _currentTarget.position);
        }
        
        public void StopChase()
        {
            if (_agent == null) return;
            
            _agent.isStopped = true;
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


