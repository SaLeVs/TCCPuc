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
        
        
        private void Start()
        {
            if (!IsServer) return;

            hitbox.Initialize(damageAmountPerAttack);
            hitbox.DisableHitbox();
        }

        public void StartAttack()
        {
            if (!IsServer) return;

            hitbox.ResetHits();
            hitbox.EnableHitbox();
        }

        private void Update()
        {
            
        }

        public void EndAttack()
        {
            if (!IsServer) return;

            hitbox.DisableHitbox();
        }
    } 
}

