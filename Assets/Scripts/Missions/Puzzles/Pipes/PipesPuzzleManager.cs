using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;

namespace Missions.Puzzles
{
    public class PipesPuzzleManager : PuzzleManagerBase
    {
        [SerializeField] private List<float> possibleAngles;

        [SerializeField] private Transform spawnListRoot;
        [SerializeField] private GameObject defaultPipePrefab;
        [SerializeField] private List<PipeGridLayout> possibleGridLayouts;
        [SerializeField] private List<PipeSpawnConfig> pipeConfigs = new();

        public List<float> PossiblePipesAngles => possibleAngles;


        protected override void SpawnPuzzle()
        {
            ResolvePipeConfigsFromGridIfNeeded();
            SpawnPipes();
        }

        private void ResolvePipeConfigsFromGridIfNeeded()
        {
            if (possibleGridLayouts == null || possibleGridLayouts.Count == 0)
            {
                return;
            }

            if (spawnListRoot == null)
            {
                Debug.LogWarning("PipesPuzzleManager: possibleGridLayouts defined, but spawnListRoot is null. Using manual pipeConfigs.");
                return;
            }

            PipeGridLayout selectedLayout = possibleGridLayouts[Random.Range(0, possibleGridLayouts.Count)];

            List<PipeSpawnConfig> resolvedConfigs = PipeGridResolver.BuildSpawnConfigs(selectedLayout, spawnListRoot, defaultPipePrefab);

            if (resolvedConfigs.Count > 0)
            {
                pipeConfigs = resolvedConfigs;
            }
            else
            {
                Debug.LogWarning($"PipesPuzzleManager: The layout '{selectedLayout.name}' did not generate any valid config. Using manual pipeConfigs.");
            }
        }

        private void SpawnPipes()
        {
            List<int> randomSteps = GenerateRandomSteps(pipeConfigs.Count);

            for (int i = 0; i < pipeConfigs.Count; i++)
            {
                PipeSpawnConfig config = pipeConfigs[i];

                if (config.prefab == null)
                {
                    Debug.LogWarning($"Pipe {i} prefab null");
                    continue;
                }

                if (config.spawnPoint == null || config.spawnPoint.Count == 0)
                {
                    Debug.LogWarning($"Pipe {i} spawnpoint null");
                    continue;
                }

                PipeTotem pipe = SpawnPiece<PipeTotem>(config.prefab, config.spawnPoint[0]);

                if (pipe == null) continue;

                pipe.Setup(config.correctSteps, randomSteps[i]);
            }
        }

        private List<int> GenerateRandomSteps(int pipeCount)
        {
            int wrongCount = Mathf.CeilToInt(pipeCount / 2f);

            List<int> indices = new();

            for (int i = 0; i < pipeCount; i++)
            {
                indices.Add(i);
            }

            Shuffle(indices);

            int[] steps = new int[pipeCount];

            for (int i = 0; i < pipeCount; i++)
            {
                int configIndex = indices[i];

                PipeSpawnConfig config = pipeConfigs[configIndex];

                bool shouldStartWrong = i < wrongCount;

                steps[configIndex] = shouldStartWrong ? GetWrongStep(config, possibleAngles) : GetCorrectStep(config);
            }

            return new List<int>(steps);
        }

        private int GetCorrectStep(PipeSpawnConfig config)
        {
            if (config.correctSteps == null || config.correctSteps.Count == 0) return 0;
            return config.correctSteps[Random.Range(0, config.correctSteps.Count)];
        }

        private int GetWrongStep(PipeSpawnConfig config, List<float> angles)
        {
            List<int> wrongSteps = new();

            for (int i = 0; i < angles.Count; i++)
            {
                if (!config.correctSteps.Contains(i))
                {
                    wrongSteps.Add(i);
                }
            }

            if (wrongSteps.Count == 0) return 0;
            return wrongSteps[Random.Range(0, wrongSteps.Count)];
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }


        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                foreach (PuzzlePieceBase piece in Pieces)
                {
                    if (piece is PipeTotem pipe && pipe != null)
                    {
                        pipe.Uninitialize();
                    }
                }
            }

            base.OnNetworkDespawn();
        }

    }
}
