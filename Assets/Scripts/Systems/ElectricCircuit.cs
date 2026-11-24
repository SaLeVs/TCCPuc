using System;
using Components.Sound;
using Interfaces;
using Monster;
using Monster.MonsterSabotages;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class ElectricCircuit : NetworkBehaviour, IInteractable
    {
        public event Action OnCircuitRestored;

        [Tooltip("How long the panel refuses a second pull, so holding the key does not spam it.")]
        [SerializeField] private float interactCooldown = 1f;

        [SerializeField] private SoundDefinitionSO restoredSound;

        private MonsterSabotage _monsterSabotage;
        private float _nextInteractTime;

        public bool CanInteract(GameObject interactor)
        {
            // The blackout is a replicated flag (MonsterSabotage.LightsOut), so a client can answer this
            // as well as the host. It used to answer true outright, and clients saw the panel offer
            // itself with the lights on, then do nothing when used.
            return HasBrokenLights();
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out NetworkObject networkObject)) return false;
            if (!networkObject.IsOwner) return false;

            RestoreLightsRpc();
            return true;
        }

        [Rpc(SendTo.Server)]
        private void RestoreLightsRpc()
        {
            if (Time.time < _nextInteractTime) return;
            _nextInteractTime = Time.time + interactCooldown;

            MonsterSabotage sabotage = ResolveSabotage();
            if (sabotage == null) return;

            int restored = sabotage.RestoreAll(SabotageType.Light);
            if (restored == 0) return;

            WorldSound.Play(restoredSound, transform.position, NetworkObject);
            NotifyRestoredRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyRestoredRpc()
        {
            OnCircuitRestored?.Invoke();
        }

        private bool HasBrokenLights()
        {
            MonsterSabotage sabotage = ResolveSabotage();

            return sabotage != null && sabotage.HasSabotagedOfType(SabotageType.Light);
        }

        private MonsterSabotage ResolveSabotage()
        {
            if (_monsterSabotage == null)
            {
                _monsterSabotage = FindFirstObjectByType<MonsterSabotage>();
            }

            return _monsterSabotage;
        }
    }
}
