using System.Collections.Generic;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionTotemGroup : NetworkBehaviour
    {
        [SerializeField] private List<MissionTotem> totems;
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;

        public bool IsComplete { get; private set; }

        
        public override void OnNetworkSpawn()
        {
            foreach (MissionTotem totem in totems)
            {
                totem.OnTotemDeposited += OnTotemDeposited;
                totem.Initialize(this, ownershipSelector);
            }
        }

        
        private void OnTotemDeposited(ulong clientId)
        {
            if (!IsServer) return;
            if (!CheckAllSlotsCorrect()) return;

            IsComplete = true;
            missionCompleter.Complete();
            NotifyMissionCompletedRpc();
            NotifyOwnerMissionCompletedRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        private bool CheckAllSlotsCorrect()
        {
            foreach (MissionTotem totem in totems)
            {
                if (!totem.IsSlotCorrect)
                {
                    return false;
                }
            }

            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyMissionCompletedRpc()
        {
            Debug.Log("MissionTotemGroup: Mission completed");
        }

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
            foreach (MissionTotem totem in totems)
            {
                totem.OnTotemDeposited -= OnTotemDeposited;
                totem.Uninitialize();
            }
        }
        
    }
}