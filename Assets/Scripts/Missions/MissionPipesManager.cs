using System;
using System.Collections.Generic;
using Enums;
using Interfaces;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionPipesManager : MissionsManagerBase, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;
        
        [SerializeField] private MissionCompleter missionCompleter;
        
        [SerializeField] private List<float> possibleAngles;
        
        [SerializeField] private Transform spawnListRoot;
        [SerializeField] private GameObject defaultPipePrefab;
        [SerializeField] private List<PipeGridLayout> possibleGridLayouts;
        
        public override bool IsComplete { get; protected set; }
        public List<float> PossiblePipesAngles => possibleAngles;
        
        private readonly List<PipeTotem> _spawnedPipes = new();
        private List<PipeSpawnConfig> pipeConfigs = new();
        
        public void RequestSpawn()
        {
            if (!IsServer) return;

            ResolvePipeConfigsFromGridIfNeeded();
            SpawnPipes();
        }
        
        
        private void ResolvePipeConfigsFromGridIfNeeded()
        {
            if (possibleGridLayouts == null || possibleGridLayouts.Count == 0) return;
            if (spawnListRoot == null)
            {
                Debug.LogWarning("MissionPipesManager: possibleGridLayouts defined, but spawnListRoot is null. Using manual pipeConfigs.");
                return;
            }

            PipeGridLayout selectedLayout = possibleGridLayouts[UnityEngine.Random.Range(0, possibleGridLayouts.Count)];

            List<PipeSpawnConfig> resolvedConfigs = PipeGridResolver.BuildSpawnConfigs(selectedLayout, spawnListRoot, defaultPipePrefab);

            if (resolvedConfigs.Count > 0)
            {
                pipeConfigs = resolvedConfigs;
            }
            else
            {
                Debug.LogWarning($"MissionPipesManager: The layout '{selectedLayout.name}' did not generate any valid config. Using manual pipeConfigs.");
            }
        }
        
        private void SpawnPipes()
        {
            List<int> randomSteps = GenerateRandomSteps(pipeConfigs.Count);
    
            for (int i = 0; i < pipeConfigs.Count; i++)
            {
                PipeSpawnConfig config = pipeConfigs[i];

                foreach ((GameObject prefab, Transform spawnPoint) assignment in SpawnUtility.GenerateSpawnAssignments(config))
                {
                    GameObject spawned = Instantiate(assignment.prefab, assignment.spawnPoint.position, assignment.spawnPoint.rotation);

                    if (spawned.TryGetComponent(out NetworkObject networkObject))
                    {
                        networkObject.Spawn();
                    }

                    if (spawned.TryGetComponent(out PipeTotem pipe))
                    {
                        pipe.Initialize(this, possibleAngles, config.correctSteps, randomSteps[i]);
                        _spawnedPipes.Add(pipe);
                    }
                }
            }

            OnSpawnCompleted?.Invoke();
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
            return config.correctSteps[UnityEngine.Random.Range(0, config.correctSteps.Count)];
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
            return wrongSteps[UnityEngine.Random.Range(0, wrongSteps.Count)];
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        
        public void OnPipeRotated(ulong clientId)
        {
            if (!IsServer || IsComplete) return;
            if (!CheckAllPipesCorrect()) return;

            IsComplete = true;
            missionCompleter.Complete();
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
        
        private bool CheckAllPipesCorrect()
        {
            foreach (PipeTotem pipe in _spawnedPipes)
            {
                if (!pipe.IsCorrect) return false;
            }
            return true;
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc() => Debug.Log("MissionPipesManager: Mission completed!");

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyOwnerMissionCompletedRpc(RpcParams rpcParams = default)
        {
            NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetworkObject.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompletePersonalMission(OwnershipSelector.Mission);
            }
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            foreach (PipeTotem pipe in _spawnedPipes)
            {
                if (pipe == null) continue;

                pipe.Uninitialize();

                if (pipe.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }

                Destroy(pipe.gameObject);
            }

            _spawnedPipes.Clear();
        }
        
    }
}