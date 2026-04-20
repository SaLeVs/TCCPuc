using System;
using Components;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerDead : NetworkBehaviour
    {
        public event Action<bool> OnDeathEvent;

        [SerializeField] private Health playerHealth;

        public bool IsDead => _isDead.Value;
        
        private NetworkVariable<bool> _isDead;

        
        public override void OnNetworkSpawn()
        {
            _isDead.OnValueChanged += PlayerDead_OnIsDeadChanged;
            
            if (IsServer)
            {
                playerHealth.OnDie += PlayerHealth_OnDie;
            }
        }

        
        private void PlayerHealth_OnDie(Health health)
        {
            _isDead.Value = true;
        }

        private void PlayerDead_OnIsDeadChanged(bool previous, bool current)
        {
            OnDeathEvent?.Invoke(current);
        }

        
        public override void OnNetworkDespawn()
        {
            _isDead.OnValueChanged -= PlayerDead_OnIsDeadChanged;

            if (IsServer)
            {
                playerHealth.OnDie -= PlayerHealth_OnDie; 
            }
        }
        
    }
}

