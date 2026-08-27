using System.Collections.Generic;
using UnityEngine;

namespace Missions
{
    public static class SpawnUtility
    {
        public static List<(GameObject prefab, Transform spawnPoint)> GenerateSpawnAssignments(SpawnConfig config)
        {
            List<(GameObject, Transform)> result = new();

            if (config == null) return result;
            if (config.prefabs == null || config.prefabs.Length == 0) return result;
            if (config.spawnPoints == null || config.spawnPoints.Length == 0) return result;

            List<Transform> availableSpawns = new(config.spawnPoints);

            foreach (GameObject prefab in config.prefabs)
            {
                if (availableSpawns.Count == 0) break;

                int index = Random.Range(0, availableSpawns.Count);

                Transform selectedSpawn = availableSpawns[index];

                availableSpawns.RemoveAt(index);

                result.Add((prefab, selectedSpawn));
            }

            return result;
        }
    }
}