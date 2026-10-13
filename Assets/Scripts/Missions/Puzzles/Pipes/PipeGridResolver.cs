using System.Collections.Generic;
using System.Linq;
using ScriptableObjects;
using UnityEngine;

namespace Missions.Puzzles
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

            List<Transform> orderedSpawnPoints = GetOrderedSpawnPoints(spawnListRoot);

            // Encaixado: cada célula tem a própria posição, local ao SpawnList. Senão, cai na grade do SpawnList.
            bool fitted = layout.HasFittedPositions;

            if (!fitted)
            {
                Debug.LogWarning($"PipeGridResolver: layout '{layout.name}' is not fitted (or changed after fitting). " +
                                 "Using the SpawnList grid, pipes may not connect. Press 'Encaixar spawns deste grid' on the layout.");
            }

            foreach (PipeCellData cell in layout.Cells)
            {
                Transform spawnPoint = GetSpawnPoint(orderedSpawnPoints, cell.row, cell.column, layout.columns);

                if (spawnPoint == null && !fitted)
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
                    spawnPoint = spawnPoint != null ? new List<Transform> { spawnPoint } : new List<Transform>(),
                    correctSteps = (cell.correctSteps != null && cell.correctSteps.Count > 0) ? new List<int>(cell.correctSteps) : new List<int> { 0 },
                    fittedPose = fitted
                        ? new Pose(spawnListRoot.TransformPoint(cell.spawnPosition), spawnListRoot.rotation)
                        : null
                };

                configs.Add(config);
            }

            return configs;
        }


        internal static List<Transform> GetOrderedSpawnPoints(Transform spawnListRoot)
        {
            List<Transform> points = new();

            for (int i = 0; i < spawnListRoot.childCount; i++)
            {
                points.Add(spawnListRoot.GetChild(i));
            }

            points.Sort((a, b) => ExtractNumber(a.name).CompareTo(ExtractNumber(b.name)));

            return points;
        }

        private static int ExtractNumber(string objectName)
        {
            string digits = new string(objectName.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int number) ? number : int.MaxValue;
        }

        private static Transform GetSpawnPoint(List<Transform> orderedSpawnPoints, int row, int column, int columns)
        {
            int index = row * columns + column;

            if (index < 0 || index >= orderedSpawnPoints.Count) return null;

            return orderedSpawnPoints[index];
        }
    }
}