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
        private readonly HashSet<NetworkObject> _hitTargets = new();

        private readonly NetworkVariable<bool> _hitboxActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public override void OnNetworkSpawn()
        {
            _hitboxActive.OnValueChanged += OnHitboxActiveChanged;
            hitboxCollider.enabled = _hitboxActive.Value;
        }

        private void OnHitboxActiveChanged(bool previous, bool current)
        {
            hitboxCollider.enabled = current;
            
            if (current)
            {
                _hitTargets.Clear();
            }
        }

        public void Initialize(float damage)
        {
            _damage = damage;
        }

        public void EnableHitbox()
        {
            if (!IsServer) return;
            _hitboxActive.Value = true;
        }

        public void DisableHitbox()
        {
            if (!IsServer) return;
            _hitboxActive.Value = false;
        }

        public void ResetHits()
        {
            _hitTargets.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null || _hitTargets.Contains(netObj)) return;

            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;

            _hitTargets.Add(netObj);
            health.TakeDamage(_damage);
        }

        public override void OnNetworkDespawn()
        {
            _hitboxActive.OnValueChanged -= OnHitboxActiveChanged;
        }
    }
}