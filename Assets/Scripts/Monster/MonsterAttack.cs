using System;
using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterAttack : NetworkBehaviour
    {
        public event Action OnAttackStartedAnimation;
        public event Action OnAttackEndedAnimation;
        
        [SerializeField] private float distanceToAttack;
        [SerializeField] private float damageAmountPerAttack;
        [SerializeField] private float attackCooldown;
        [SerializeField] private float attackDuration;
        [SerializeField] private MonsterHitbox hitbox;

        public float DistanceToAttack => distanceToAttack;
        public float AttackCooldown => attackCooldown;
        
        private float _timer;
        private bool _isAttacking;


        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            hitbox.Initialize(damageAmountPerAttack);
            hitbox.DisableHitbox();
        }

        public void StartAttack()
        {
            if (!IsServer) return;
            
            _timer = 0f;
            _isAttacking = true;
            
            OnAttackStartedAnimation?.Invoke();
            hitbox.ResetHits();
            hitbox.EnableHitbox();
            
        }

        private void Update()
        {
            if (!IsServer) return;
            if (!_isAttacking) return;

            _timer += Time.deltaTime;

            if (_timer >= attackDuration)
            {
                EndAttack();
            }
        }

        private void EndAttack()
        {
            if (!IsServer) return;
            
            OnAttackEndedAnimation?.Invoke();
            _isAttacking = false;
            hitbox.DisableHitbox();
        }
        
    } 
}

