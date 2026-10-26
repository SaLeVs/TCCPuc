using System;
using System.Collections.Generic;
using Components;
using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterHitbox : NetworkBehaviour
    {
        /// <summary>Server only: this swing's damage landed on someone.</summary>
        public event Action OnTargetHit;

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
            OnTargetHit?.Invoke();

            // The glitch belongs on the screen of whoever was hit. It used to fire from
            // MonsterAttack.TryStartAttack, which only runs here on the server, so the host saw it
            // on every swing - misses and other players' hits included - and the victim never did.
            if (netObj.IsPlayerObject)
            {
                PlayHitGlitchRpc(RpcTarget.Single(netObj.OwnerClientId, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PlayHitGlitchRpc(RpcParams rpcParams = default)
        {
            if (VHSGlitchController.Instance != null)
                VHSGlitchController.Instance.TriggerGlitch();
        }

        public override void OnNetworkDespawn()
        {
            _hitboxActive.OnValueChanged -= OnHitboxActiveChanged;
        }
    }
}