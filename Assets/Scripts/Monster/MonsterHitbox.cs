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

            hitboxCollider.enabled = true;
        }

        public void DisableHitbox()
        {
            hitboxCollider.enabled = false;
        }

        public void ResetHits()
        {
            _hitTargets.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            NetworkObject networkObject = other.GetComponentInParent<NetworkObject>();

            if (_hitTargets.Contains(networkObject)) return;

            if (other.TryGetComponent(out Health health))
            {
                _hitTargets.Add(networkObject);
                health.TakeDamage(_damage);
                Debug.Log("Hitbox hit " + networkObject.name);
            }
        }
    }
}