using System;
using System.Collections.Generic;
using Components.Sound;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class PlayerMissionHolder : NetworkBehaviour
    {
        [SerializeField] private PlayerMissionHolderUi _playerMissionHolderUi;

        [Header("HUD sounds")]
        [SerializeField] private SoundDefinitionSO missionReceivedSound;
        [SerializeField] private SoundDefinitionSO missionCompletedSound;

        public event Action<MissionSO> OnPersonalMissionReceived;
        public event Action<MissionSO> OnPersonalMissionCompleted;
        public event Action<MissionSO> OnMainMissionReceived;
        public event Action<MissionSO> OnMainMissionCompleted;
        public event Action<string> OnMessageReceived;

        /// <summary>Any mission, personal or main, reached this machine's own player. Owner-side only.</summary>
        public static event Action OnLocalMissionReceived;

        /// <summary>This machine's own player finished a mission, personal or main. Owner-side only.</summary>
        public static event Action OnLocalMissionCompleted;

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
            AnnounceReceived();
        }
        
        public void CompletePersonalMission(MissionSO mission)
        {
            if (!IsOwner) return;
            if (!_personalMissions.Contains(mission)) return;

            _personalMissions.Remove(mission);
            OnPersonalMissionCompleted?.Invoke(mission);
            AnnounceCompleted();
        }

        public void ReceiveMainMission(MissionSO mission)
        {
            if (!IsOwner) return;

            _mainMission = mission;
            OnMainMissionReceived?.Invoke(mission);
            AnnounceReceived();
        }

        public void CompleteMainMission()
        {
            if (!IsOwner) return;
            if (_mainMission == null) return;
            
            MissionSO completed = _mainMission;
            _mainMission = null;
            OnMainMissionCompleted?.Invoke(completed);
            AnnounceCompleted();
        }

        private void AnnounceReceived()
        {
            OnLocalMissionReceived?.Invoke();
            UiSound.Play(missionReceivedSound);
        }

        private void AnnounceCompleted()
        {
            OnLocalMissionCompleted?.Invoke();
            UiSound.Play(missionCompletedSound);
        }
        
        [Rpc(SendTo.Owner)]
        public void SendMessageRpc(string message)
        {
            OnMessageReceived?.Invoke(message);
        }
        
        [Rpc(SendTo.Owner)]
        public void ClearPersonalMissionsRpc()
        {
            _personalMissions.Clear();
        }
        
        public bool HasPersonalMission(MissionSO mission)
        {
            return _personalMissions.Contains(mission);
        }
    } 
}

