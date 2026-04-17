using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterAttack : NetworkBehaviour
    {
        [SerializeField] private float distanceToAttack;
        [SerializeField] private float damageAmountPerAttack;
        [SerializeField] private float attackCooldown;
        [SerializeField] private float attackDuration;
        [SerializeField] private MonsterHitbox hitbox;

        public float DistanceToAttack => distanceToAttack;
        public float AttackCooldown => attackCooldown;
        
        private float _timer;
        private bool _isAttacking;
        
        private void Start()
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
            
            hitbox.ResetHits();
            hitbox.EnableHitbox();
            Debug.Log("Attack started");
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

        public void EndAttack()
        {
            if (!IsServer) return;
            
            _isAttacking = false;
            hitbox.DisableHitbox();
            Debug.Log("Attack ended");
        }
        
    } 
}

