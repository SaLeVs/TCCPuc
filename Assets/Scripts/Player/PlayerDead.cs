using System;
using Components;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerDead : NetworkBehaviour
    {
        public event Action<bool> OnDeathEvent;
        public event Action<Transform> OnRagdollSpawned;
        public static Action<Vector3> OnDeathSound;

        [SerializeField] private Health playerHealth;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private Transform ragdollSpawnRoot;
        [SerializeField] private GameObject ragdollPrefab;
        
        [SerializeField] private LayerMask deadLayer;
        [SerializeField] private LayerMask aliveLayer;

        public bool IsDead => _isDead.Value;
        
        private NetworkVariable<bool> _isDead = new NetworkVariable<bool>();
        private GameObject _spawnedRagdoll;
        private bool _deathHandled;

        
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

            if (current)
            {
                HandleDeathLocally();
                OnDeathSound?.Invoke(transform.position);
            }

            OnDeathEvent?.Invoke(current);
        }
        
        private void HandleDeathLocally()
        {
            if (_deathHandled) return;

            _deathHandled = true;

            SpawnRagdoll();
            DisableLivingBody();
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
        
        private void DisableLivingBody()
        {
            if (playerAnimator != null) playerAnimator.enabled = false;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }
        
        private void SpawnRagdoll()
        {
            Transform spawnRoot = ragdollSpawnRoot != null ? ragdollSpawnRoot : transform;
            _spawnedRagdoll = Instantiate(ragdollPrefab, spawnRoot.position, spawnRoot.rotation);

            if (_spawnedRagdoll.TryGetComponent(out PlayerRagdoll ragdoll))
            {
                ragdoll.InitializeFrom(spawnRoot);
                OnRagdollSpawned?.Invoke(ragdoll.HeadBone);
            }

            ApplyDeadLayerToRagdoll(_spawnedRagdoll);
        }
        
        private void ApplyDeadLayerToRagdoll(GameObject ragdoll)
        {
            int layerIndex = LayerMaskToLayer(deadLayer);
            SetLayerRecursively(ragdoll, layerIndex);
        }
        
        
        public override void OnNetworkDespawn()
        {
            _isDead.OnValueChanged -= PlayerDead_OnIsDeadChanged;

            if (IsServer)
            {
                playerHealth.OnDie -= PlayerHealth_OnDie; 
            }
            
            if (_spawnedRagdoll != null)
            {
                Destroy(_spawnedRagdoll);
            }
            
        }
        
        
    }
}

