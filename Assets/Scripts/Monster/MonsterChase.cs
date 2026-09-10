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
        public static Action<Vector3> OnMonsterSeeTargetSound;
        
        [SerializeField] private float chaseSpeed = 8f;
        [SerializeField] private float targetReevaluationInterval = 1f;
        [SerializeField] private float rotationSpeed = 10f;

        [Tooltip("Within this distance the monster turns to face the target directly instead of " +
                 "following the path. Keep it around attack range so the swing lands square.")]
        [SerializeField, Min(0f)] private float faceTargetDistance = 3f;
        
        public List<Transform> monsterTargets;
        public float DistanceFromTarget => _currentDistanceFromTarget;
        public bool HasTarget => _currentTarget != null;
        public float ChaseSpeed => chaseSpeed;
        
        private NavMeshAgent _agent;
        private MonsterBrain _monsterBrain;
        private List<Transform> _monsterTargets;
        private Transform _currentTarget;
        private float _currentDistanceFromTarget = float.MaxValue;
        private float _reevaluationTimer;
        
        
        public void Initialize(List<Transform> monsterTargetsList, NavMeshAgent agent, MonsterBrain monsterBrain)
        {
            monsterTargets = monsterTargetsList;
            _monsterTargets = monsterTargetsList;
            _agent = agent;
            _monsterBrain = monsterBrain;

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
                return;
            }
            
            if (_monsterBrain != null && _monsterBrain.IsTrackingLostTarget) return;

            ClearTarget();
        }
        
        public void ForgetTarget() => ClearTarget();
        
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
            {
                SetTarget(bestTarget);
            }
        }
        
        private void SetTarget(Transform target)
        {
            if (!target || !_agent) return;
            
            bool wasWithoutTarget = _currentTarget == null;

            _currentTarget = target;

            if (wasWithoutTarget)
            {
                PlaySpottedSoundClientRpc();
            }

            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        private void PlaySpottedSoundClientRpc()
        {
            OnMonsterSeeTargetSound?.Invoke(transform.position);
        }
        
        private void ClearTarget()
        {
            _currentTarget = null;
            _currentDistanceFromTarget = float.MaxValue;

            if (_agent == null) return;

            _agent.isStopped = true;
            _agent.ResetPath();

            // Giving up on a target can happen without ChaseState ever exiting — ForgetTarget()
            // reaches this straight from the brain when tracking goes cold. StopChase() would
            // have restored the rotation; this path used to leave the agent unable to turn.
            _agent.updateRotation = true;
        }

        public void StartChase()
        {
            if (_agent == null) return;

            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            _agent.updateRotation = false;
            OnStartedChasingAnimation?.Invoke();
        }
        
        
        public void ChaseUpdate(float deltaTime)
        {
            if (_agent == null) return;

            // Was `_currentTarget != null && !_currentTarget`, which can never both hold — the
            // destroyed-target fallback below had been unreachable. A disconnecting player left
            // the monster pathing at a dead Transform until the next reevaluation tick.
            if (!_currentTarget)
            {
                _currentTarget = null;

                Transform fallback = GetBestAvailableTarget();

                if (fallback == null)
                {
                    ClearTarget();
                    return;
                }

                SetTarget(fallback);
            }

            _reevaluationTimer += deltaTime;
            if (_reevaluationTimer >= targetReevaluationInterval)
            {
                _reevaluationTimer = 0f;
                ReevaluateTarget();
            }

            _agent.SetDestination(_currentTarget.position);

            UpdateDistanceFromTarget();
            RotateTowardsMovement(deltaTime);
        }
        
        /// <summary>
        /// Faces where the monster is actually going, not where the target is.
        ///
        /// <para>Chasing runs with <c>updateRotation = false</c>, so this is the only thing
        /// steering the body. Pointing it straight at the target meant that whenever the path
        /// wrapped around a wall the monster ran sideways along the corridor while staring
        /// through the wall at the player — which is what read as scraping and flickering
        /// against the geometry. The path direction is the honest answer while travelling;
        /// only up close, where the attack has to land square, is the target itself right.</para>
        /// </summary>
        private void RotateTowardsMovement(float deltaTime)
        {
            if (_currentTarget == null) return;

            Vector3 direction = _agent.desiredVelocity;
            direction.y = 0f;

            bool closeEnoughToFaceTarget = _currentDistanceFromTarget <= faceTargetDistance;

            // desiredVelocity collapses to nothing when the agent is stopped or has arrived,
            // and there is no path direction to read then either.
            if (closeEnoughToFaceTarget || direction.sqrMagnitude < 0.05f)
            {
                direction = _currentTarget.position - _agent.transform.position;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.01f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            _agent.transform.rotation = Quaternion.Slerp(_agent.transform.rotation, targetRotation, rotationSpeed * deltaTime);
        }

        public void StopChase()
        {
            if (_agent == null) return;

            _agent.isStopped = true;
            _agent.updateRotation = true;
            OnStoppedChasingAnimation?.Invoke();
        }
        
        public void UpdateDistanceFromTarget()
        {
            if (_currentTarget == null)
            {
                _currentDistanceFromTarget = float.MaxValue;
                return;
            }
    
            _currentDistanceFromTarget = Vector3.Distance(_agent.transform.position, _currentTarget.position);
        }
        
        
        public void Uninitialize(List<Transform> monsterTargetsList, NavMeshAgent agent, MonsterBrain monsterBrain)
        {
            monsterTargets = null;
            _monsterTargets = null;
            _currentTarget = null;
            _currentDistanceFromTarget = float.MaxValue;
            _agent = null;

            if (monsterBrain != null)
            {
                monsterBrain.OnPlayerEnterInVision -= MonsterBrain_OnPlayerEnterInVision;
                monsterBrain.OnPlayerExitInVision -= MonsterBrain_OnPlayerExitInVision;
            }
        }
        
        
    }

}


