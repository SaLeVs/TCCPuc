using System;
using System.Collections.Generic;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class LampsManager : NetworkBehaviour
    {
        public event Action OnLampsSpawned;
        
        [SerializeField] private GameObject[] lampsPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;
        
        public bool IsComplete { get; private set; }
        public MissionOwnershipSelector OwnershipSelector => ownershipSelector;

        private readonly List<LampTotem> _spawnedLamps = new();
        private readonly Dictionary<LampTotem, bool> _requiredStates = new();
        
        
        public void RequestSpawnLamps()
        {
            if (!IsServer) return;
            
            SpawnLamps();
        }

        private void SpawnLamps()
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                GameObject spawned = Instantiate(lampsPrefab[i], spawnPoints[i].position, spawnPoints[i].rotation);

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
            
            OnLampsSpawned?.Invoke();
        }

        private void LampTotem_OnLampToggled(ulong clientId, bool toggled)
        {
            if (!IsServer) return;
            if (IsComplete) return;
            if (!CheckAllLampsCorrect()) return;

            IsComplete = true;

            missionCompleter.Complete();
            
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        private bool CheckAllLampsCorrect()
        {
            foreach (LampTotem lamp in _spawnedLamps)
            {
                bool requiredState = _requiredStates[lamp];

                if (lamp.IsOn != requiredState)
                    return false;
            }

            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc() => Debug.Log("MissionTotemGroup: Mission completed");

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

