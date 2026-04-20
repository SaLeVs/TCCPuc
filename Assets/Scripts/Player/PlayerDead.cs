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
        [SerializeField] private LayerMask deadLayer;
        [SerializeField] private LayerMask aliveLayer;

        public bool IsDead => _isDead.Value;
        
        private NetworkVariable<bool> _isDead = new NetworkVariable<bool>();

        
        public override void OnNetworkSpawn()
        {
            _isDead.OnValueChanged += PlayerDead_OnIsDeadChanged;
            
            if (IsServer)
            {
                playerHealth.OnDie += PlayerHealth_OnDie;
            }
            
            ApplyLayerState(_isDead.Value);
            
        }
        
        
        private void PlayerHealth_OnDie(Health health)
        {
            _isDead.Value = true;
        }
        
        private void PlayerDead_OnIsDeadChanged(bool previous, bool current)
        {
            ApplyLayerState(current);
            OnDeathEvent?.Invoke(current);
        }
        
        private void ApplyLayerState(bool dead)
        {
            int layerIndex = LayerMaskToLayer(dead ? deadLayer : aliveLayer);
            SetLayerRecursively(gameObject, layerIndex);
        }

        private void SetLayerRecursively(GameObject playerObject, int layer)
        {
            playerObject.layer = layer;

            foreach (Transform child in playerObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private int LayerMaskToLayer(LayerMask mask)
        {
            int layer = mask.value;
            int layerIndex = 0;

            while (layer > 1)
            {
                layer >>= 1;
                layerIndex++;
            }

            return layerIndex;
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

