using System;
using Interfaces;
using Monster;
using Monster.MonsterSabotages;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class ElectricCircuit : NetworkBehaviour, IInteractable
    {
        public static Action<Vector3> OnCircuitRestoredSound;
        public event Action OnCircuitRestored;

        [Tooltip("How long the panel refuses a second pull, so holding the key does not spam it.")]
        [SerializeField] private float interactCooldown = 1f;

        private MonsterSabotage _monsterSabotage;
        private float _nextInteractTime;

        public bool CanInteract(GameObject interactor)
        {
            if (!IsServer) return true;

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

            Debug.Log($"ElectricCircuit: {restored} light(s) back on.");

            PlayRestoredRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayRestoredRpc()
        {
            OnCircuitRestoredSound?.Invoke(transform.position);
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
