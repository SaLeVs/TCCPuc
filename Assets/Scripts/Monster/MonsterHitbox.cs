using System.Collections.Generic;
using Components;
using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterHitbox : NetworkBehaviour
    {
        [SerializeField] private Collider hitboxCollider;

        private float _damage;
        private HashSet<NetworkObject> _hitTargets = new();

        
        public void Initialize(float damage)
        {
            _damage = damage;
        }

        public void EnableHitbox()
        {
            if (!IsServer) return;

            hitboxCollider.enabled = true;
        }

        public void DisableHitbox()
        {
            if (!IsServer) return;

            hitboxCollider.enabled = false;
        }

        public void ResetHits()
        {
            _hitTargets.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            if (!other.TryGetComponent(out NetworkObject netObj)) return;

            if (_hitTargets.Contains(netObj)) return;

            if (other.TryGetComponent(out Health health))
            {
                _hitTargets.Add(netObj);
                health.TakeDamage(_damage);
                Debug.Log("Hitbox hit " + netObj.name);
            }
        }
    }
}