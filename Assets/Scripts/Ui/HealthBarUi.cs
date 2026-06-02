using Components;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HealthBarUi : NetworkBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Image lifeFill;


        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                health.OnHealthChanged += Health_OnHealthChanged;
                
                float initial = health.MaxHealth;
                UpdateBar(initial);
            }
        }
        
        
        private void Health_OnHealthChanged(float currentHealth)
        {
            UpdateBar(currentHealth);
        }

        private void UpdateBar(float current)
        {
            lifeFill.fillAmount = current / health.MaxHealth;
        }


        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                health.OnHealthChanged -= Health_OnHealthChanged;
            }
        }
        
        
    }
}

