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
        [SerializeField] private List<PipeSpawnConfig> pipeConfigs;
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;
        
        public bool IsComplete { get; private set; }
        
        private readonly List<MissionPipe> _spawnedPipes = new();
        
        private const int MAX_PIPES_ROTATIONS = 4;
    
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

                if (spawnedPipe.TryGetComponent(out MissionPipe pipe))
                {
                    pipe.Initialize(this, ownershipSelector, randomSteps[i]);
                    _spawnedPipes.Add(pipe);
                }

                if (spawnedPipe.TryGetComponent(out NetworkObject networkObject))
                {
                    networkObject.Spawn();
                }
            }

            OnSpawnCompleted?.Invoke();
        }
        
        private List<int> GenerateRandomSteps(int pipeCount)
        {
            int wrongCount = Mathf.CeilToInt(pipeCount / 2f);
            
            List<int> indices = new();
            for (int i = 0; i < pipeCount; i++) indices.Add(i);
            Shuffle(indices);

            int[] steps = new int[pipeCount];

            for (int i = 0; i < pipeCount; i++)
            {
                int configIndex = indices[i];
                PipeType pipeType = pipeConfigs[configIndex].pipeType;

                steps[configIndex] = i < wrongCount ? GetWrongStep(pipeType) : UnityEngine.Random.Range(0, MAX_PIPES_ROTATIONS);
            }

            return new List<int>(steps);
        }
        
        private int GetWrongStep(PipeType pipeType)
        {
            switch (pipeType)
            {
                case PipeType.Straight:
                    return 1;
                case PipeType.Joint:
                    return 1;
                default:
                    return 1;
            }
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
            foreach (MissionPipe pipe in _spawnedPipes)
            {
                if (!pipe.IsCorrect)
                {
                    return false;
                }
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
                missionHolder.CompletePersonalMission(ownershipSelector.Mission);
            }
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            foreach (MissionPipe pipe in _spawnedPipes)
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

