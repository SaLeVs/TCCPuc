using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public class Health : NetworkBehaviour, IDamageable
    {
        public event Action<Health> OnDie;
        
        [field: SerializeField] public float MaxHealth {get; private set;}
        
        public NetworkVariable<float> currentHealth = new NetworkVariable<float>();
        private bool _isDead;
    
    
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
        
            currentHealth.Value = MaxHealth;
        }

        public void TakeDamage(float damage)
        {
            ModifyHealth(damage);
            Debug.Log($"TakeDamage on {gameObject.name}, damage: {damage}");
        }

        public void RestoreHealth(float heal)
        {
            ModifyHealth(-heal);
        }

        private void ModifyHealth(float value)
        {
            if (_isDead) return;
        
            float newHealth = currentHealth.Value - value;
            currentHealth.Value = Mathf.Clamp(newHealth, 0f, MaxHealth);
            Debug.Log($"ModifyHealth on {gameObject.name}, new health: {newHealth}");
            
            if (currentHealth.Value <= 0f)
            {
                OnDie?.Invoke(this);
                _isDead = true; 
            }
        }
        
    } 
}


