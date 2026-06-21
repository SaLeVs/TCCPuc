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
        
        [SerializeField] private MissionCompleter missionCompleter;
        [SerializeField] private List<SpawnConfig> totemConfigs;
        [SerializeField] private List<SpawnConfig> pickableConfigs;

        public override bool IsComplete { get; protected set; }
        
        private readonly List<MissionTotem> _spawnedTotems = new();
        private readonly List<NetworkObject> _spawnedPickables = new();
        
        
        public void RequestSpawn()
        {
            if (!IsServer) return;
            
            SpawnTotems();
            SpawnPickables(); 
        }

        private void SpawnTotems()
        {
            foreach (SpawnConfig config in totemConfigs)
            {
                if (config.prefab == null || config.spawnPoint == null) continue;

                GameObject spawned = Instantiate(config.prefab, config.spawnPoint.position, config.spawnPoint.rotation);

                if (spawned.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }

                if (spawned.TryGetComponent(out MissionTotem totem))
                {
                    totem.Initialize(this);
                    totem.OnTotemDeposited += MissionTotem_OnTotemDeposited;
                    _spawnedTotems.Add(totem);
                }
            }

            OnSpawnCompleted?.Invoke();
        }

        private void SpawnPickables()
        {
            foreach (SpawnConfig config in pickableConfigs)
            {
                if (config.prefab == null || config.spawnPoint == null) continue;

                GameObject spawned = Instantiate(config.prefab, config.spawnPoint.position, config.spawnPoint.rotation);

                if (spawned.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();

                    if (spawned.TryGetComponent(out IMissionOwnerAware ownerAware))
                    {
                        ownerAware.SetOwnershipSelector(this);
                        MissionItemRegistry.Instance?.Register(ownerAware.ItemId, this);
                    }

                    _spawnedPickables.Add(netObj);
                }
            }
        }

        private void MissionTotem_OnTotemDeposited(ulong clientId)
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
                
                totem.OnTotemDeposited -= MissionTotem_OnTotemDeposited;
                
                if (totem.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                
                Destroy(totem.gameObject);
            }
            _spawnedTotems.Clear();

            foreach (NetworkObject netObj in _spawnedPickables) 
            {
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn();
            }
            _spawnedPickables.Clear();
        }

        
        
    }
}