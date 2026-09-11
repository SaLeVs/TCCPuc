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

        /// <summary>
        /// True while the monster is still recovering from its last swing.
        ///
        /// <para>The cooldown lives here, on the component, and not in AttackState — that state is
        /// entered and left every time the player crosses the attack range, so a cooldown stored
        /// there was wiped on the way out. Stepping back and forward granted a free hit every
        /// time, which made dancing in and out strictly better than standing still.</para>
        /// </summary>
        public bool IsOnCooldown => _cooldownRemaining > 0f;

        public float CooldownRemaining => _cooldownRemaining;

        private float _timer;
        private float _cooldownRemaining;
        private bool _isAttacking;
        private NavMeshAgent _agent;
        private ObstacleAvoidanceType _defaultAvoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance;


        public override void OnNetworkSpawn()
        {
            hitbox.Initialize(damageAmountPerAttack);
            hitbox.DisableHitbox();
        }

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
            _defaultAvoidance = agent.obstacleAvoidanceType;
        }
        
        /// <summary>
        /// Swings, unless already swinging or still on cooldown. Returns whether it actually
        /// started, so callers can keep asking every frame without having to track the rhythm.
        /// </summary>
        public bool TryStartAttack()
        {
            if (_isAttacking) return false;
            if (_cooldownRemaining > 0f) return false;

            _timer = 0f;
            _isAttacking = true;

            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

            OnMonsterAttackSound?.Invoke(transform.position);
            OnAttackStartedAnimation?.Invoke();
            hitbox.ResetHits();
            hitbox.EnableHitbox();

            return true;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
            }

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
            _cooldownRemaining = attackCooldown;
            _agent.isStopped = false;

            // Was setting NoObstacleAvoidance again, same as StartAttack — so the first attack
            // switched avoidance off for good and the monster stopped steering around anything.
            _agent.obstacleAvoidanceType = _defaultAvoidance;

            hitbox.DisableHitbox();
        }

        public void CancelAttack()
        {
            bool wasSwinging = _isAttacking;

            _isAttacking = false;
            hitbox.DisableHitbox();
            _agent.isStopped = false;
            _agent.obstacleAvoidanceType = _defaultAvoidance;

            // An interrupted swing still costs a cooldown, otherwise stepping out mid-animation
            // is the same free-hit exploit by another route. But leaving the state while already
            // cooling down must not top the timer back up.
            if (wasSwinging) _cooldownRemaining = attackCooldown;
        }
        
    } 
}

