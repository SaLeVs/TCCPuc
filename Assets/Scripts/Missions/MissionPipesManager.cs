using System;
using System.Collections.Generic;
using Enums;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionPipesManager : NetworkBehaviour, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;
        
        [SerializeField] private GameObject pipeTotemStraight;
        [SerializeField] private GameObject pipeTotemJoint;
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;
        
        [SerializeField] private List<PipeSpawnConfig> pipeConfigs;
        [SerializeField] private List<float> possibleAngles;
        
        public bool IsComplete { get; private set; }
        
        private readonly List<PipeTotem> _spawnedPipes = new();
    
        
        public void RequestSpawn()
        {
            if (!IsServer) return;
            SpawnPipes();
        }
        
        private void SpawnPipes()
        {
            List<int> randomSteps = GenerateRandomSteps(pipeConfigs.Count);
    
            for (int i = 0; i < pipeConfigs.Count; i++)
            {
                PipeSpawnConfig config = pipeConfigs[i];
                GameObject pipePrefab = config.pipeType == PipeType.Straight ? pipeTotemStraight : pipeTotemJoint;
                GameObject spawnedPipe = Instantiate(pipePrefab, config.spawnPoint.position, config.spawnPoint.rotation);

                if (spawnedPipe.TryGetComponent(out NetworkObject networkObject))
                {
                    networkObject.Spawn();
                }

                if (spawnedPipe.TryGetComponent(out PipeTotem pipe))
                {
                    pipe.Initialize(this, ownershipSelector, possibleAngles, config.correctSteps, randomSteps[i]);
                    
                    _spawnedPipes.Add(pipe);
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
            if (!IsServer) return;
            if (IsComplete) return;
            if (!CheckAllPipesCorrect()) return;

            IsComplete = true;
            missionCompleter.Complete();
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
        
        private bool CheckAllPipesCorrect()
        {
            bool allCorrect = true;

            foreach (PipeTotem pipe in _spawnedPipes)
            {
                bool isCorrect = pipe.IsCorrect;

                Debug.Log($"Pipe: {pipe.gameObject.name} | " + $"Step: {pipe.CurrentStep} | " + $"Correct: {isCorrect}");

                if (!isCorrect)
                {
                    allCorrect = false;
                }
            }

            return allCorrect;
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc() => Debug.Log("MissionPipesManager: Mission completed!");

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyOwnerMissionCompletedRpc(RpcParams rpcParams = default)
        {
            NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetworkObject.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompletePersonalMission(ownershipSelector.Mission);
            }
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            foreach (PipeTotem pipe in _spawnedPipes)
            {
                if (pipe == null) continue;

                pipe.Uninitialize();

                if (pipe.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }

                Destroy(pipe.gameObject);
            }

            _spawnedPipes.Clear();
        }
        
    }
}

