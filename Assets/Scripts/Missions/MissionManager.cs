using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Interfaces;
using Missions.PersonalMissions;
using Player;
using ScriptableObjects;
using Systems;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Missions
{
    public class MissionManager : NetworkBehaviour
    {
        public event Action OnMainMissionCompleted;
        
        [SerializeField] private ContractsSO currentContract;
        
        public ContractsSO CurrentContract => currentContract;
        
        private Dictionary<ulong, List<MissionSO>> _personalMissionsForPlayers = new Dictionary<ulong, List<MissionSO>>();
        
        private readonly List<MissionSO> _allMissionsAtContract = new List<MissionSO>();
        private IReadOnlyList<ulong> _playersInGame;
        
        private int _completedPersonalMissions;
        private int _totalPersonalMissions;
        
        private bool _missionsDistributed;
        private bool _roomsSpawned;
        private bool _ownersAssigned;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                MissionCompleter.OnMissionCompleted += MissionCompleter_OnMissionCompleted;
                PlayerTracker.Instance.OnAllPlayersConnected += DistributeMissionsForPlayers;
                PlayerDropper.OnItemDropped += PlayerDropper_OnItemDropped;
            }
        }


        public void HandlePlayerDeath(ulong deadClientId)
        {
            if (!IsServer) return;

            if (!_personalMissionsForPlayers.TryGetValue(deadClientId, out List<MissionSO> deadMissions) || deadMissions.Count == 0)
            {
                Debug.Log($"MissionManager: Player {deadClientId} died with no pending missions.");
                return;
            }

            List<ulong> alivePlayers = GetAlivePlayers(deadClientId);

            if (alivePlayers.Count == 0)
            {
                Debug.LogWarning("MissionManager: No alive players to receive transferred missions.");
                return;
            }

            int total = deadMissions.Count;
            
            Dictionary<ulong, List<MissionSO>> transferMap = new();

            for (int i = 0; i < deadMissions.Count; i++)
            {
                ulong targetId = alivePlayers[i % alivePlayers.Count];

                if (!_personalMissionsForPlayers.ContainsKey(targetId))
                {
                    _personalMissionsForPlayers[targetId] = new List<MissionSO>();
                }

                _personalMissionsForPlayers[targetId].Add(deadMissions[i]);
                SendPersonalMissionRpc(deadMissions[i].missionID, RpcTarget.Single(targetId, RpcTargetUse.Temp));

                if (!transferMap.ContainsKey(targetId))
                {
                    transferMap[targetId] = new List<MissionSO>();
                }
                
                transferMap[targetId].Add(deadMissions[i]);
            }

            StartCoroutine(ReassignOwnersNextFrame(transferMap));

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(deadClientId, out NetworkClient deadClient) && deadClient.PlayerObject != null && deadClient.PlayerObject.TryGetComponent(out PlayerMissionHolder deadHolder))
            {
                deadHolder.ClearPersonalMissionsRpc();
            }

            deadMissions.Clear();
            Debug.Log($"MissionManager: Transferred {total} missions from player {deadClientId} to {alivePlayers.Count} player(s).");
        }

        private IEnumerator ReassignOwnersNextFrame(Dictionary<ulong, List<MissionSO>> transferMap)
        {
            yield return null; 

            foreach (var (targetId, missions) in transferMap)
            {
                ReassignInteractableOwners(missions, targetId);
            }
        }

        private void ReassignInteractableOwners(List<MissionSO> missions, ulong newOwnerId)
        {
            MissionOwnershipSelector[] selectors = FindObjectsByType<MissionOwnershipSelector>(FindObjectsSortMode.None);

            foreach (MissionSO mission in missions)
            {
                bool found = false;

                foreach (MissionOwnershipSelector selector in selectors)
                {
                    if (selector.Mission.missionID == mission.missionID)
                    {
                        selector.AssignOwner(newOwnerId);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Debug.LogWarning($"MissionManager: No selector found for transferred mission {mission.missionName}!");
                }
            }
        }
        
        private void MissionCompleter_OnMissionCompleted(MissionSO mission)
        {
            _completedPersonalMissions++;
            RemoveMissionFromTracker(mission);
            Debug.Log("MissionManager: Mission completed");

            if (_completedPersonalMissions >= _totalPersonalMissions)
            {
                RevealMainMission();
            }
        }
        
        private List<ulong> GetAlivePlayers(ulong excludeId)
        {
            List<ulong> playersAlive = new List<ulong>();

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (clientId == excludeId) continue;
                if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) continue;
                if (client.PlayerObject == null) continue;
                
                if (client.PlayerObject.TryGetComponent(out PlayerDead playerDead) && !playerDead.IsDead)
                {
                    playersAlive.Add(clientId);
                }
            }

            return playersAlive;
        }
        
        private void RemoveMissionFromTracker(MissionSO mission)
        {
            foreach (var missions in _personalMissionsForPlayers.Values)
            {
                if (missions.Remove(mission)) break;
            }
        }
        
        private void RevealMainMission()
        {
            Debug.Log($"MissionManager: Reveal main mission {currentContract.mainMission.missionName}");
            
            if (IsServer)
            {
                GameObject mainMission = Instantiate(currentContract.mainMissionPrefab);
                
                if (mainMission.TryGetComponent(out NetworkObject missionNetworkObject))
                {
                    missionNetworkObject.Spawn();
                }
                if (mainMission.TryGetComponent(out MainMission missionScript))
                {
                    missionScript.StartMission();
                }
            }

            RevealMainMissionRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RevealMainMissionRpc()
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.ReceiveMainMission(currentContract.mainMission);
            }
        }

        private void DistributeMissionsForPlayers()
        {
            _allMissionsAtContract.Clear();
            _allMissionsAtContract.AddRange(currentContract.personalMissions);

            _playersInGame = NetworkManager.Singleton.ConnectedClientsIds;

            if (!ValidateMissionPool()) return;

            ShuffleMissions();
            AssignMissionsToAllPlayers();

            _missionsDistributed = true;
            TryAssignInteractableOwners();
        }
        
        private bool ValidateMissionPool()
        {
            if (_allMissionsAtContract.Count == 0) return false;
            if (_playersInGame.Count == 0) return false;
 
            return true;
        }
        
        private void ShuffleMissions()
        {
            for (int i = _allMissionsAtContract.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (_allMissionsAtContract[i], _allMissionsAtContract[randomIndex]) = (_allMissionsAtContract[randomIndex], _allMissionsAtContract[i]);
            }
        }
        
        private void AssignMissionsToAllPlayers()
        {
            int totalMissions = _allMissionsAtContract.Count; 
            int totalPlayers = _playersInGame.Count;
            int baseCount = totalMissions / totalPlayers;
            int remainder = totalMissions % totalPlayers;
 
            int missionIndex = 0;
 
            foreach (ulong clientId in _playersInGame)
            {
                int count = baseCount + (remainder-- > 0 ? 1 : 0);
 
                List<MissionSO> assigned = new List<MissionSO>();
 
                for (int i = 0; i < count; i++)
                {
                    assigned.Add(_allMissionsAtContract[missionIndex++]);
                }
 
                _personalMissionsForPlayers[clientId] = assigned;
                _totalPersonalMissions += assigned.Count;
 
                SendMissionsToPlayer(clientId, assigned);
            }
        }

        private void SendMissionsToPlayer(ulong clientId, List<MissionSO> missions)
        {
            foreach (MissionSO mission in missions)
            {
                SendPersonalMissionRpc(mission.missionID, RpcTarget.Single(clientId, RpcTargetUse.Temp)); 
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendPersonalMissionRpc(int missionID, RpcParams rpcParams = default)
        {
            MissionSO mission = currentContract.GetMissionByID(missionID);
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.ReceivePersonalMission(mission);
            }
        }

        public void OnRoomsSpawned()
        {
            _roomsSpawned = true;

            IMissionSpawnable[] spawnables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IMissionSpawnable>().ToArray();

            foreach (IMissionSpawnable spawnable in spawnables)
            {
                spawnable.RequestSpawn();
            }

            TryAssignInteractableOwners();
        }

        private void TryAssignInteractableOwners()
        {
            if (_ownersAssigned) return;
            if (!_missionsDistributed || !_roomsSpawned) return;

            AssignInteractableOwners();
            _ownersAssigned = true;
        }
        
        private void AssignInteractableOwners()
        {
            MissionOwnershipSelector[] selectors = FindObjectsByType<MissionOwnershipSelector>(FindObjectsSortMode.None);
            HashSet<MissionOwnershipSelector> alreadyAssigned = new HashSet<MissionOwnershipSelector>();
            
            foreach (ulong clientId in _personalMissionsForPlayers.Keys)
            {
                foreach (MissionSO mission in _personalMissionsForPlayers[clientId])
                {
                    bool found = false;
                    
                    foreach (MissionOwnershipSelector selector in selectors)
                    {
                        if (alreadyAssigned.Contains(selector)) continue;

                        if (selector.Mission.missionID == mission.missionID)
                        {
                            found = true;
                            selector.AssignOwner(clientId);
                            alreadyAssigned.Add(selector);
                            break; 
                        }
                    }

                    if (!found)
                    {
                        Debug.LogWarning($"MissionManager: No selector found for mission {mission.missionName}!");
                    }
                }
            }
        }
        
        private void PlayerDropper_OnItemDropped(GameObject spawned, int itemId)
        {
            if (!spawned.TryGetComponent(out IMissionOwnerAware ownerAware)) return;
            if (MissionItemRegistry.Instance == null) return;
            if (!MissionItemRegistry.Instance.TryGetManager(itemId, out MissionsManagerBase manager)) return;

            ownerAware.SetOwnershipSelector(manager);
        }
        
        public void CompleteMainMission()
        {
            if (!IsServer) return;

            OnMainMissionCompleted?.Invoke();
            Debug.Log("MissionManager: Main mission completed!");
            CompleteMainMissionRpc();
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        private void CompleteMainMissionRpc()
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(NetworkManager.Singleton.LocalClientId);

            if (playerNetObj.TryGetComponent(out PlayerMissionHolder missionHolder))
            {
                missionHolder.CompleteMainMission();
            }
        }
        
        public void SendMessageToAllPlayers(string message)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) continue;
                if (client.PlayerObject == null) continue;
                if (!client.PlayerObject.TryGetComponent(out PlayerMissionHolder holder)) continue;
                holder.SendMessageRpc(message);
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                MissionCompleter.OnMissionCompleted -= MissionCompleter_OnMissionCompleted;
                PlayerDropper.OnItemDropped -= PlayerDropper_OnItemDropped;

                if (PlayerTracker.Instance != null)
                {
                    PlayerTracker.Instance.OnAllPlayersConnected -= DistributeMissionsForPlayers;
                }
            }
        }
        
    }
}