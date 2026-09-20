using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class Health : NetworkBehaviour, IDamageable
    {
        public event Action<Health> OnDie;
        public event Action<float> OnHealthChanged;
        public static Action<Vector3> OnDamageSound;

        /// <summary>
        /// Every health change on every client, with the old and new value.
        ///
        /// <para>OnDie and the damage RPC are raised from ModifyHealth, which only ever runs
        /// on the server, so neither is usable by client-side systems. The backing
        /// NetworkVariable does replicate, and this rides its change callback - so a listener
        /// on any client sees any player getting hurt or dying, not just the host and not just
        /// its own player. Check IsOwner on the sender to tell yours apart from the rest.</para>
        /// </summary>
        public static event Action<Health, float, float> OnAnyHealthChanged;
        
        [field: SerializeField] public float MaxHealth {get; private set;}
        
        public NetworkVariable<float> currentHealth = new NetworkVariable<float>();
        private bool _isDead;
    
    
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                currentHealth.Value = MaxHealth;
            }
            
            currentHealth.OnValueChanged += CurrentHealth_OnValueChanged;
        }

        private void CurrentHealth_OnValueChanged(float previousValue, float newValue)
        {
            OnHealthChanged?.Invoke(currentHealth.Value);
            OnAnyHealthChanged?.Invoke(this, previousValue, newValue);
        }

        public void TakeDamage(float damage)
        {
            if (IsServer)
            {
                ModifyHealth(damage);
                return;
            }

            TakeDamageServerRpc(damage);
        }

        [Rpc(SendTo.Server)]
        private void TakeDamageServerRpc(float damage)
        {
            // Used to open with `if (IsServer) return;`. SendTo.Server only ever delivers to the
            // server, so IsServer was always true here and the method never did anything —
            // damage reported by a client was silently thrown away. Note that RestoreHealth had
            // no such guard, so healing from a client worked while damage did not.
            ModifyHealth(damage);
        }

        public void RestoreHealth(float heal)
        {
            if (IsServer)
            {
                ModifyHealth(-heal);
            }
            else
            {
                RestoreHealthServerRpc(heal);
            }
        }

        [Rpc(SendTo.Server)]
        private void RestoreHealthServerRpc(float heal)
        {
            ModifyHealth(-heal);
        }

        private void ModifyHealth(float value)
        {
            if (_isDead) return;

            float previousHealth = currentHealth.Value;
            
            float newHealth = currentHealth.Value - value;
            currentHealth.Value = Mathf.Clamp(newHealth, 0f, MaxHealth);
            
            if (value > 0f && currentHealth.Value < previousHealth)
            {
                DamageClientRpc();
            }

            if (currentHealth.Value <= 0f)
            {
                _isDead = true;
                OnDie?.Invoke(this);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void DamageClientRpc()
        {
            OnDamageSound?.Invoke(new Vector3(transform.position.x, transform.position.y + 0.6f, transform.position.z));
        }
        
        public override void OnNetworkDespawn()
        {
            currentHealth.OnValueChanged -= CurrentHealth_OnValueChanged;
        }
        
    } 
}


