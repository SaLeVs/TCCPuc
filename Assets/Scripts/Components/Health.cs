using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class Health : NetworkBehaviour, IDamageable
    {
        public Action<Health> OnDie;
        
        [field: SerializeField] public float MaxHealth {get; private set;}
        
        public NetworkVariable<float> currentHealth = new NetworkVariable<float>();
        private bool _isDead;
    
    
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
        
            currentHealth.Value = 0;
        }

        public void TakeDamage(float damage)
        {
            ModifyHealth(damage);
        }

        public void RestoreHealth(float heal)
        {
            ModifyHealth(-heal);
        }

        private void ModifyHealth(float value)
        {
            if (_isDead) return;
        
            float newHealth = currentHealth.Value + value;
            currentHealth.Value = Mathf.Clamp(newHealth, 0f, MaxHealth);
        
            if (currentHealth.Value >= MaxHealth)
            {
                OnDie?.Invoke(this);
                _isDead = true; 
            }
        }
        
    } 
}


