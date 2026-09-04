using System;
using System.Collections.Generic;
using Interfaces;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Systems
{
    /// <summary>
    /// Places the electric circuit once the rooms exist. Implements IMissionSpawnable, so
    /// MissionManager.OnRoomsSpawned already calls it — the same hook the mission props use, which
    /// is the only moment the room spawn points are guaranteed to be in the scene.
    /// </summary>
    public class ElectricCircuitManager : NetworkBehaviour, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;

        [SerializeField] private GameObject circuitPrefab;

        [Tooltip("Extra points outside the spawned rooms. The manager also finds every CircuitSpawnPoint in the scene.")]
        [SerializeField] private CircuitSpawnPoint[] fallbackSpawnPoints;

        public ElectricCircuit SpawnedCircuit { get; private set; }

        public void RequestSpawn()
        {
            if (!IsServer) return;

            if (circuitPrefab == null)
            {
                Debug.LogError("ElectricCircuitManager: no circuit prefab assigned.");
                return;
            }

            CircuitSpawnPoint point = PickSpawnPoint();

            if (point == null)
            {
                Debug.LogWarning("ElectricCircuitManager: no CircuitSpawnPoint found; the lights will have no way back on.");
                return;
            }

            GameObject spawned = Instantiate(circuitPrefab, point.SpawnTransform.position, point.SpawnTransform.rotation);

            if (spawned.TryGetComponent(out NetworkObject networkObject))
            {
                networkObject.Spawn();
            }

            spawned.TryGetComponent(out ElectricCircuit circuit);
            SpawnedCircuit = circuit;

            Debug.Log($"ElectricCircuitManager: circuit placed at {point.name}.");

            OnSpawnCompleted?.Invoke();
        }

        private CircuitSpawnPoint PickSpawnPoint()
        {
            List<CircuitSpawnPoint> candidates = new List<CircuitSpawnPoint>();

            // Rooms are instantiated moments before this runs, so their points only exist now.
            candidates.AddRange(FindObjectsByType<CircuitSpawnPoint>(FindObjectsSortMode.None));

            foreach (CircuitSpawnPoint point in fallbackSpawnPoints)
            {
                if (point != null && !candidates.Contains(point)) candidates.Add(point);
            }

            if (candidates.Count == 0) return null;

            return candidates[Random.Range(0, candidates.Count)];
        }
    }
}
