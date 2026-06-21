using System;
using System.Collections.Generic;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Missions
{
    public class LampsManager : MissionsManagerBase, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;
        public event Action<int, int> OnCorrectLampsCountChanged;
        public event Action<bool> OnMissionCompleteChanged;
        
        [SerializeField] private GameObject lampPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private MissionCompleter missionCompleter;
        [SerializeField] private LampsFeedback lampsFeedback;
        
        public override bool IsComplete { get; protected set; }
        public int CorrectLampsCount => _correctLampsCount.Value;
        public int TotalLampsCount => _totalLampsCount.Value;

        private NetworkVariable<int> _correctLampsCount = new NetworkVariable<int>(0);
        private NetworkVariable<int> _totalLampsCount = new NetworkVariable<int>(0);
        private NetworkVariable<bool> _isMissionComplete = new NetworkVariable<bool>(false);

        private readonly List<LampTotem> _spawnedLamps = new();
        private readonly Dictionary<LampTotem, bool> _requiredStates = new();
        
        
        public override void OnNetworkSpawn()
        {
            _correctLampsCount.OnValueChanged += HandleCorrectCountChanged;
            _totalLampsCount.OnValueChanged += HandleTotalCountChanged;
            _isMissionComplete.OnValueChanged += HandleMissionCompleteChanged;
            
            OnCorrectLampsCountChanged?.Invoke(_correctLampsCount.Value, _totalLampsCount.Value);
            OnMissionCompleteChanged?.Invoke(_isMissionComplete.Value);
        }
        
        private void HandleCorrectCountChanged(int previousValue, int newValue)
        {
            OnCorrectLampsCountChanged?.Invoke(newValue, _totalLampsCount.Value);
        }

        private void HandleTotalCountChanged(int previousValue, int newValue)
        {
            OnCorrectLampsCountChanged?.Invoke(_correctLampsCount.Value, newValue);
        }

        private void HandleMissionCompleteChanged(bool previousValue, bool newValue)
        {
            OnMissionCompleteChanged?.Invoke(newValue);
        }
        
        public void RequestSpawn()
        {
            if (!IsServer) return;
            
            SpawnLamps();
        }
        
        private void SpawnLamps()
        {
            int totalLamps = spawnPoints.Length;
            int wrongCount = CalculateWrongLampsCount(totalLamps);

            List<int> shuffledIndices = new List<int>(totalLamps);
            for (int i = 0; i < totalLamps; i++)
                shuffledIndices.Add(i);

            ShuffleIndices(shuffledIndices);

            HashSet<int> wrongIndices = new HashSet<int>();
            for (int i = 0; i < wrongCount; i++)
                wrongIndices.Add(shuffledIndices[i]);

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                GameObject spawned = Instantiate(lampPrefab, spawnPoints[i].position, spawnPoints[i].rotation);

                if (spawned.TryGetComponent(out NetworkObject netObj))
                    netObj.Spawn();

                if (spawned.TryGetComponent(out LampTotem lamp))
                {
                    lamp.OnLampToggled += LampTotem_OnLampToggled;
                    _spawnedLamps.Add(lamp);

                    bool shouldBeOn = Random.Range(0, 2) == 0;
                    bool startOn = wrongIndices.Contains(i) ? !shouldBeOn : shouldBeOn;

                    _requiredStates.Add(lamp, shouldBeOn);
                    lamp.Initialize(this, startOn);
                }
            }

            _totalLampsCount.Value = _spawnedLamps.Count;
            RecalculateCorrectLampsCount();
            OnSpawnCompleted?.Invoke();
        }
        
        private int CalculateWrongLampsCount(int totalLamps)
        {
            if (totalLamps <= 0) return 0;

            int minWrong = Mathf.CeilToInt(totalLamps * 0.5f); 
            int maxWrong = totalLamps;                         

            return Random.Range(minWrong, maxWrong + 1);
        }

        private void ShuffleIndices(List<int> indices)
        {
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
        }
        
        private void LampTotem_OnLampToggled(ulong clientId, bool toggled)
        {
            if (!IsServer || IsComplete) return;

            RecalculateCorrectLampsCount();

            if (_correctLampsCount.Value < _spawnedLamps.Count) return;

            IsComplete = true;
            _isMissionComplete.Value = true;
            missionCompleter.Complete();
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
        
        private void RecalculateCorrectLampsCount()
        {
            int correct = 0;
            
            foreach (LampTotem lamp in _spawnedLamps)
            {
                if (lamp.IsOn == _requiredStates[lamp])
                {
                    correct++;
                }
            }
            
            _correctLampsCount.Value = correct;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc() => Debug.Log("LampManager: Mission Complete!");

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyOwnerMissionCompletedRpc(RpcParams rpcParams = default)
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompletePersonalMission(OwnershipSelector.Mission);
            }
        }
        
        
        public override void OnNetworkDespawn()
        {
            _correctLampsCount.OnValueChanged -= HandleCorrectCountChanged;
            _isMissionComplete.OnValueChanged -= HandleMissionCompleteChanged;
            _totalLampsCount.OnValueChanged -= HandleTotalCountChanged;

            if (!IsServer) return;

            foreach (LampTotem lamp in _spawnedLamps)
            {
                if (lamp == null) continue;

                lamp.OnLampToggled -= LampTotem_OnLampToggled;

                if (lamp.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }

                Destroy(lamp.gameObject);
            }

            _spawnedLamps.Clear();
            _requiredStates.Clear();
        }
        
    }
}

