using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;

namespace Missions
{
    public static class PipeGridResolver
    {
        public static List<PipeSpawnConfig> BuildSpawnConfigs(PipeGridLayout layout, Transform spawnListRoot, GameObject defaultPrefab)
        {
            List<PipeSpawnConfig> configs = new();

            if (layout == null)
            {
                Debug.LogWarning("PipeGridResolver: Layout null, none config generated.");
                return configs;
            }

            if (spawnListRoot == null)
            {
                Debug.LogWarning("PipeGridResolver: SpawnListRoot null, none config generated.");
                return configs;
            }

            foreach (PipeCellData cell in layout.Cells)
            {
                Transform spawnPoint = GetSpawnPoint(spawnListRoot, cell.row, cell.column);

                if (spawnPoint == null)
                {
                    Debug.LogWarning($"PipeGridResolver: Spawn point not found for column {cell.column}, row {cell.row}. Skipping this cell.");
                    continue;
                }

                GameObject prefab = cell.prefabOverride != null ? cell.prefabOverride : defaultPrefab;

                if (prefab == null) 
                {
                    Debug.LogWarning($"PipeGridResolver: None prefab found for column {cell.column}, row {cell.row}. Skipping this cell.");
                    continue;
                }
                
                PipeSpawnConfig config = new PipeSpawnConfig
                {
                    prefab = prefab,
                    spawnPoint = new List<Transform> { spawnPoint },
                    correctSteps = (cell.correctSteps != null && cell.correctSteps.Count > 0) ? new List<int>(cell.correctSteps) : new List<int> { 0 }
                };

                configs.Add(config);
            }

            return configs;
        }

        private static Transform GetSpawnPoint(Transform spawnListRoot, int row, int column)
        {
            if (row < 0 || row >= spawnListRoot.childCount) return null;

            Transform line = spawnListRoot.GetChild(row);

            if (column < 0 || column >= line.childCount) return null;

            return line.GetChild(column);
        }
        
    }
}