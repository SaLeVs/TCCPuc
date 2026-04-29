using System.Collections.Generic;
using ScriptableObjects;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class MissionManager : NetworkBehaviour
    {
        [SerializeField] private ContractsSO currentContract;
        [SerializeField] private int individualMissionsPerPlayer;
        
        private Dictionary<ulong, List<MissionSO>> _personalMissions = new();

        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                DistributePersonalMissions();
            }
        }

        private void DistributePersonalMissions()
        {
            List<MissionSO> personalMissionsList = new List<MissionSO>(currentContract.personalMissions);

            if (personalMissionsList.Count == 0)
            {
                Debug.LogError("MissionManager: None personal mission in contract.");
                return;
            }

            for (int i = personalMissionsList.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (personalMissionsList[i], personalMissionsList[randomIndex]) = (personalMissionsList[randomIndex], personalMissionsList[i]);
            }

            IReadOnlyList<ulong> playersInGame = NetworkManager.Singleton.ConnectedClientsIds;
            int missionListIndex = 0;

            foreach (ulong clientId in playersInGame)
            {
                List<MissionSO> missionsAssigned = new List<MissionSO>();

                for (int i = 0; i < individualMissionsPerPlayer; i++)
                {
                    missionsAssigned.Add(personalMissionsList[missionListIndex % personalMissionsList.Count]);
                    missionListIndex++;
                }

                _personalMissions[clientId] = missionsAssigned;

                foreach (MissionSO mission in missionsAssigned)
                {
                    SendPersonalMissionRpc(mission.missionID, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                }
            }
            
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SendPersonalMissionRpc(int missionID, RpcParams rpcParams = default)
        {
            MissionSO mission = currentContract.GetMissionByID(missionID);
            Debug.Log($"MissionManager: Mission received - {mission.missionName}");
            Debug.Log($"MissionManager: Mission received - {mission.instructions}");
        }
        
        
        public void OnRoomsSpawned()
        {
            
        }
        
    }
}