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

        // The health on show: this player's own, or the one of whoever they spectate.
        private Health _shown;


        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Show(health);
            }
        }

        /// <summary>
        /// Shows <paramref name="other"/>'s health instead of this player's own - a spectator sees
        /// the life of the player they watch. Health replicates, so it follows every hit. Null goes
        /// back to the own health.
        /// </summary>
        public void Watch(Health other) => Show(other != null ? other : health);

        private void Show(Health target)
        {
            if (_shown == target) return;

            if (_shown != null) _shown.OnHealthChanged -= Health_OnHealthChanged;

            _shown = target;

            if (_shown == null) return;

            _shown.OnHealthChanged += Health_OnHealthChanged;

            // Before the server's first write the value still reads 0: a fresh player is at full health,
            // a dead one really is at 0.
            Player.PlayerState state = _shown.GetComponentInParent<Player.PlayerState>();
            bool dead = state != null && state.IsDead;
            float current = _shown.currentHealth.Value > 0f || dead ? _shown.currentHealth.Value : _shown.MaxHealth;

            UpdateBar(current);
        }
        
        
        private void Health_OnHealthChanged(float currentHealth)
        {
            UpdateBar(currentHealth);
        }

        private void UpdateBar(float current)
        {
            if (_shown == null) return;

            lifeFill.fillAmount = current / Mathf.Max(1f, _shown.MaxHealth);
        }


        public override void OnNetworkDespawn()
        {
            if (_shown != null)
            {
                _shown.OnHealthChanged -= Health_OnHealthChanged;
                _shown = null;
            }
        }
    }
}
