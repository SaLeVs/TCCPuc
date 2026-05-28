using System;
using System.Collections.Generic;
using Interfaces;
using Missions.PersonalMissions;
using Systems;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionTotemGroup : MissionsManagerBase, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;

        [SerializeField] private GameObject[] totemPrefabs;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private MissionCompleter missionCompleter;

        public override bool IsComplete { get; protected set; }
        
        private readonly List<MissionTotem> _spawnedTotems = new();
        
        
        public void RequestSpawn()
        {
            if (!IsServer) return;
            
            SpawnTotems();
        }

        private void SpawnTotems()
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                GameObject spawned = Instantiate(totemPrefabs[i], spawnPoints[i].position, spawnPoints[i].rotation);

                if (spawned.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }

                if (spawned.TryGetComponent(out MissionTotem totem))
                {
                    totem.Initialize(this);
                    totem.OnTotemDeposited += OnTotemDeposited;
                    _spawnedTotems.Add(totem);
                }
            }
            
            OnSpawnCompleted?.Invoke();
        }

        private void OnTotemDeposited(ulong clientId)
        {
            if (!IsServer || IsComplete) return;
            if (!CheckAllSlotsCorrect()) return;

            IsComplete = true;
            missionCompleter.Complete();
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        private bool CheckAllSlotsCorrect()
        {
            foreach (MissionTotem totem in _spawnedTotems)
            {
                if (!totem.IsSlotCorrect) return false;
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
                missionHolder.CompletePersonalMission(OwnershipSelector.Mission);
            }
        }

        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            foreach (MissionTotem totem in _spawnedTotems)
            {
                if (totem == null) continue;
                
                totem.OnTotemDeposited -= OnTotemDeposited;
                
                if (totem.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                
                Destroy(totem.gameObject);
            }

            _spawnedTotems.Clear();
        }

        
        
    }
}