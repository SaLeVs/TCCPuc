using System;
using System.Collections.Generic;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class PlayerMissionHolder : NetworkBehaviour
    {
        [SerializeField] private PlayerMissionHolderUi _playerMissionHolderUi;
        
        public event Action<MissionSO> OnPersonalMissionReceived;
        public event Action<MissionSO> OnPersonalMissionCompleted;
        public event Action<MissionSO> OnMainMissionReceived;

        private readonly List<MissionSO> _personalMissions = new List<MissionSO>();
        private MissionSO _mainMission;

        public IReadOnlyList<MissionSO> PersonalMissions => _personalMissions;
        public MissionSO MainMission => _mainMission;

        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
                
            _playerMissionHolderUi.Initialize(this);
        }
        
        public void ReceivePersonalMission(MissionSO mission)
        {
            if (!IsOwner) return;

            _personalMissions.Add(mission);
            OnPersonalMissionReceived?.Invoke(mission);
            Debug.Log($"PlayerMissionHolder: Personal mission received {mission.missionName}");
        }
        
        public void CompletePersonalMission(MissionSO mission)
        {
            if (!IsOwner) return;
            if (!_personalMissions.Contains(mission)) return;

            _personalMissions.Remove(mission);
            OnPersonalMissionCompleted?.Invoke(mission);
            Debug.Log($"PlayerMissionHolder: Personal mission completed {mission.missionName}");
        }

        public void ReceiveMainMission(MissionSO mission)
        {
            if (!IsOwner) return;

            _mainMission = mission;
            OnMainMissionReceived?.Invoke(mission);
            Debug.Log($"PlayerMissionHolder: Main mission revealed: {mission.missionName}");
        }

        [Rpc(SendTo.Owner)]
        public void ClearPersonalMissionsRpc()
        {
            _personalMissions.Clear();
            Debug.Log("PlayerMissionHolder: Missions cleared after transfer.");
        }
        
        public bool HasPersonalMission(MissionSO mission)
        {
            return _personalMissions.Contains(mission);
        }
    } 
}

