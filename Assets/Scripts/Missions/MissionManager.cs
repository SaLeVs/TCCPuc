using System.Collections.Generic;
using Missions.PersonalMissions;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionManager : NetworkBehaviour
    {
        [SerializeField] private ContractsSO currentContract;
        [SerializeField] private int individualMissionsPerPlayer;
        
        public ContractsSO CurrentContract => currentContract;
        
        private Dictionary<ulong, List<MissionSO>> _personalMissionsForPlayers = new Dictionary<ulong, List<MissionSO>>();
        
        private readonly List<MissionSO> _allMissionsAtContract = new List<MissionSO>();
        private IReadOnlyList<ulong> _playersInGame;
        
        private int _completedPersonalMissions;
        private int _totalPersonalMissions;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                MissionCompleter.OnMissionCompleted += MissionCompleter_OnMissionCompleted;
                DistributeMissionsForPlayers();
            }
        }
        
        private void MissionCompleter_OnMissionCompleted(MissionSO mission)
        {
            _completedPersonalMissions++;
            Debug.Log("MissionManager: All missions completed");

            if (_completedPersonalMissions >= _totalPersonalMissions)
            {
                RevealMainMission();
            }
        }
        
        private void RevealMainMission()
        {
            Debug.Log("MissionManager: Reveal main mission");
        }

        private void DistributeMissionsForPlayers()
        {
            _allMissionsAtContract.Clear();
            _allMissionsAtContract.AddRange(currentContract.personalMissions);
            
            _playersInGame = NetworkManager.Singleton.ConnectedClientsIds;
            
            if (!ValidateMissionPool()) return;
            
            ShuffleMissions();
            AssignMissionsToAllPlayers();
        }
        
        private bool ValidateMissionPool()
        {
            if (_allMissionsAtContract.Count == 0) return false;

            int missionsNeeded = _playersInGame.Count * individualMissionsPerPlayer;

            if (_allMissionsAtContract.Count < missionsNeeded)
            {
                Debug.LogError(
                    $"MissionManager: Insufficient personal missions ({currentContract.contractName}). Required: {missionsNeeded}, available: {_allMissionsAtContract.Count}"
                    );
                return false;
            }

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
            foreach (ulong clientId in _playersInGame)
            {
                List<MissionSO> missionsAssigned = AssignMissionsToPlayer();
                _personalMissionsForPlayers[clientId] = missionsAssigned;
                _totalPersonalMissions += missionsAssigned.Count;
                SendMissionsToPlayer(clientId, missionsAssigned);
            }
        }

        private List<MissionSO> AssignMissionsToPlayer()
        {
            List<MissionSO> missionAssigned = new List<MissionSO>();

            for (int i = 0; i < individualMissionsPerPlayer; i++)
            {
                missionAssigned.Add(_allMissionsAtContract[0]);
                _allMissionsAtContract.RemoveAt(0);
            }

            return missionAssigned;
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
            if (!IsServer) return;

            AssignInteractableOwners();
        }

        private void AssignInteractableOwners()
        {
            MissionOwnershipSelector[] selectors = FindObjectsByType<MissionOwnershipSelector>(FindObjectsSortMode.None);

            foreach (ulong clientId in _personalMissionsForPlayers.Keys)
            {
                foreach (MissionSO mission in _personalMissionsForPlayers[clientId])
                {
                    foreach (MissionOwnershipSelector selector in selectors)
                    {
                        if (selector.Mission == mission)
                        {
                            selector.AssignOwner(clientId); ;
                        }
                    }
                }
            }
        }

        
        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                MissionCompleter.OnMissionCompleted -= MissionCompleter_OnMissionCompleted;
            }
        }
        
    }
}