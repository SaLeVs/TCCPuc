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
            // Who got hit is the server's call. The collider is enabled on every peer (the
            // NetworkVariable replicates the swing timing), so without this every client also
            // ran this handler against its own interpolated copy of the player — allocating,
            // diverging _hitTargets from the server's, and reporting hits the server never saw.
            //
            // This guard is load-bearing for the fix in Health.TakeDamageServerRpc: that RPC
            // used to return unconditionally, which is the only reason those client-side reports
            // never landed as extra damage. Do not remove one without the other.
            if (!IsServer) return;

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