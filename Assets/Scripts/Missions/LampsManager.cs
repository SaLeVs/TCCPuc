using System;
using System.Collections.Generic;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class LampsManager : NetworkBehaviour, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;
        public event Action<int, int> OnCorrectLampsCountChanged;
        public event Action<bool> OnMissionCompleteChanged;
        
        [SerializeField] private GameObject lampPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;
        [SerializeField] private LampsFeedback lampsFeedback;
        
        public bool IsComplete { get; private set; }
        public MissionOwnershipSelector OwnershipSelector => ownershipSelector;

        private NetworkVariable<int> _correctLampsCount = new NetworkVariable<int>(0);
        private NetworkVariable<int> _totalLampsCount = new NetworkVariable<int>(0);
        private NetworkVariable<bool> _isMissionComplete = new NetworkVariable<bool>(false);

        private readonly List<LampTotem> _spawnedLamps = new();
        private readonly Dictionary<LampTotem, bool> _requiredStates = new();
        
        
        public override void OnNetworkSpawn()
        {
            _correctLampsCount.OnValueChanged += HandleCorrectCountChanged;
            _isMissionComplete.OnValueChanged += HandleMissionCompleteChanged;
            
            OnCorrectLampsCountChanged?.Invoke(_correctLampsCount.Value, _totalLampsCount.Value);
            OnMissionCompleteChanged?.Invoke(_isMissionComplete.Value);
        }
        
        private void HandleCorrectCountChanged(int previousValue, int newValue)
        {
            OnCorrectLampsCountChanged?.Invoke(newValue, _totalLampsCount.Value);
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
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                GameObject spawned = Instantiate(lampPrefab, spawnPoints[i].position, spawnPoints[i].rotation);

                if (spawned.TryGetComponent(out LampTotem lamp))
                {
                    lamp.Initialize(this, ownershipSelector);
                    lamp.OnLampToggled += LampTotem_OnLampToggled;

                    _spawnedLamps.Add(lamp);

                    bool shouldBeOn = UnityEngine.Random.Range(0, 2) == 0;
                    _requiredStates.Add(lamp, shouldBeOn);
                }

                if (spawned.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }
            }
            
            _totalLampsCount.Value = _spawnedLamps.Count;
            
            OnCorrectLampsCountChanged?.Invoke(_correctLampsCount.Value, _totalLampsCount.Value);
            OnSpawnCompleted?.Invoke();
        }

        private void LampTotem_OnLampToggled(ulong clientId, bool toggled)
        {
            if (!IsServer) return;
            if (IsComplete) return;

            int correct = 0;
            
            foreach (LampTotem lamp in _spawnedLamps)
            {
                if (lamp.IsOn == _requiredStates[lamp])
                {
                    correct++;
                }
            }

            _correctLampsCount.Value = correct;

            if (correct < _spawnedLamps.Count) return;

            IsComplete = true;
            _isMissionComplete.Value = true;
            missionCompleter.Complete();
            
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc() => Debug.Log("LampManager: Mission Complete!");

        [Rpc(SendTo.SpecifiedInParams)]
        private void NotifyOwnerMissionCompletedRpc(RpcParams rpcParams = default)
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompletePersonalMission(ownershipSelector.Mission);
            }
        }

        
        public override void OnNetworkDespawn()
        {
            _correctLampsCount.OnValueChanged -= HandleCorrectCountChanged;
            _isMissionComplete.OnValueChanged -= HandleMissionCompleteChanged;

            if (!IsServer) return;

            foreach (LampTotem lamp in _spawnedLamps)
            {
                if (lamp == null) continue;

                lamp.OnLampToggled -= LampTotem_OnLampToggled;
                lamp.Uninitialize();

                if (lamp.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }

                Destroy(lamp.gameObject);
            }

            _spawnedLamps.Clear();
            _requiredStates.Clear();
        }
        
    }
}

