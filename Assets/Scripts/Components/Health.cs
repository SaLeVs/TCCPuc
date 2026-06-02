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
        
        [field: SerializeField] public float MaxHealth {get; private set;}
        
        public NetworkVariable<float> currentHealth = new NetworkVariable<float>();
        private bool _isDead;
    
    
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                currentHealth.Value = MaxHealth;
            }
        }
        
        public void TakeDamage(float damage)
        {
            Debug.Log($"TakeDamage on {gameObject.name}");
            
            if (IsServer)
            {
                ModifyHealth(damage);
            }
            else
            {
                TakeDamageServerRpc(damage);
            }
        }

        [Rpc(SendTo.Server)]
        private void TakeDamageServerRpc(float damage)
        {
            Debug.Log($"TakeDamage on {gameObject.name}");
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

            float newHealth = currentHealth.Value - value;
            currentHealth.Value = Mathf.Clamp(newHealth, 0f, MaxHealth);
            OnHealthChanged?.Invoke(currentHealth.Value);
            
            Debug.Log($"ModifyHealth on {gameObject.name}, new health: {currentHealth.Value}");

            if (currentHealth.Value <= 0f)
            {
                _isDead = true;
                OnDie?.Invoke(this);
            }
        }
        
    } 
}


