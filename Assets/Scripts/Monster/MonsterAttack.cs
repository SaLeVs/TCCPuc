using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterAttack : NetworkBehaviour
    {
        public event Action OnAttackStartedAnimation;
        public event Action OnAttackEndedAnimation;
        public static Action<Vector3> OnMonsterAttackSound;
        
        [SerializeField] private float distanceToAttack;
        [SerializeField] private float damageAmountPerAttack;
        [SerializeField] private float attackCooldown;
        [SerializeField] private float attackDuration;
        [SerializeField] private MonsterHitbox hitbox;

        public float DistanceToAttack => distanceToAttack;
        public float AttackCooldown => attackCooldown;
        
        private float _timer;
        private bool _isAttacking;
        private NavMeshAgent _agent;


        public override void OnNetworkSpawn()
        {
            hitbox.Initialize(damageAmountPerAttack);
            hitbox.DisableHitbox();
        }

        public void Initialize(NavMeshAgent agent) 
        {
            _agent = agent;
        }
        
        public void StartAttack()
        {
            _timer = 0f;
            _isAttacking = true;
            
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            
            OnMonsterAttackSound?.Invoke(transform.position);
            OnAttackStartedAnimation?.Invoke();
            hitbox.ResetHits();
            hitbox.EnableHitbox();
        }

        private void Update()
        {
            if (!_isAttacking) return;

            _timer += Time.deltaTime;

            if (_timer >= attackDuration)
            {
                EndAttack();
            }
        }

        private void EndAttack()
        {
            OnAttackEndedAnimation?.Invoke();
            _isAttacking = false;
            _agent.isStopped = false;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            hitbox.DisableHitbox();
        }
        
        public void CancelAttack()
        {
            _isAttacking = false;
            hitbox.DisableHitbox();
            _agent.isStopped = false;
        }
        
    } 
}

