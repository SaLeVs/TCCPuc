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

        /// <summary>
        /// The missions this player has, as everyone else sees them, changed. Raised on every machine,
        /// for whoever spectates them. The owner's own HUD keeps using the events above.
        /// </summary>
        public event Action OnSharedMissionsChanged;

        // The owner's missions, copied out for everyone else: the missions are handed to the owner
        // alone, so a spectator had nothing to show. By missionID, read back from the contract.
        private readonly NetworkVariable<int> _sharedMainMissionId =
            new NetworkVariable<int>(-1, writePerm: NetworkVariableWritePermission.Owner);

        private readonly NetworkList<int> _sharedPersonalMissionIds =
            new NetworkList<int>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private static MissionManager _missionManager;


        public override void OnNetworkSpawn()
        {
            _sharedMainMissionId.OnValueChanged += SharedMainMission_OnValueChanged;
            _sharedPersonalMissionIds.OnListChanged += SharedPersonalMissions_OnListChanged;

            if (!IsOwner) return;

            _playerMissionHolderUi.Initialize(this);

            // Anything handed over before the spawn goes out now.
            ShareMissions();
        }

        public override void OnNetworkDespawn()
        {
            _sharedMainMissionId.OnValueChanged -= SharedMainMission_OnValueChanged;
            _sharedPersonalMissionIds.OnListChanged -= SharedPersonalMissions_OnListChanged;
        }

        private void SharedMainMission_OnValueChanged(int previous, int current) => OnSharedMissionsChanged?.Invoke();

        private void SharedPersonalMissions_OnListChanged(NetworkListEvent<int> change) => OnSharedMissionsChanged?.Invoke();

        /// <summary>
        /// This player's missions as every machine knows them, main mission first. What a spectator's
        /// HUD shows; on the owner it is the same as <see cref="MainMission"/> and <see cref="PersonalMissions"/>.
        /// </summary>
        public void GetSharedMissions(List<MissionSO> into)
        {
            into.Clear();

            ContractsSO contract = CurrentContract();
            if (contract == null) return;

            int mainId = _sharedMainMissionId.Value;
            if (mainId >= 0 && contract.mainMission != null && contract.mainMission.missionID == mainId)
            {
                into.Add(contract.mainMission);
            }

            foreach (int id in _sharedPersonalMissionIds)
            {
                MissionSO mission = contract.GetMissionByID(id);
                if (mission != null) into.Add(mission);
            }
        }

        private static ContractsSO CurrentContract()
        {
            if (_missionManager == null) _missionManager = FindFirstObjectByType<MissionManager>();
            return _missionManager != null ? _missionManager.CurrentContract : null;
        }

        /// <summary>Owner-side: copies the missions out for everyone else after every change.</summary>
        private void ShareMissions()
        {
            if (!IsOwner || !IsSpawned) return;

            int mainId = _mainMission != null ? _mainMission.missionID : -1;
            if (_sharedMainMissionId.Value != mainId) _sharedMainMissionId.Value = mainId;

            _sharedPersonalMissionIds.Clear();

            foreach (MissionSO mission in _personalMissions)
            {
                if (mission != null) _sharedPersonalMissionIds.Add(mission.missionID);
            }
        }
        
        public void ReceivePersonalMission(MissionSO mission)
        {
            if (!IsOwner) return;

            _personalMissions.Add(mission);
            ShareMissions();
            OnPersonalMissionReceived?.Invoke(mission);
            AnnounceReceived();
        }
        
        public void CompletePersonalMission(MissionSO mission)
        {
            if (!IsOwner) return;
            if (!_personalMissions.Contains(mission)) return;

            _personalMissions.Remove(mission);
            ShareMissions();
            OnPersonalMissionCompleted?.Invoke(mission);
            AnnounceCompleted();
        }

        public void ReceiveMainMission(MissionSO mission)
        {
            if (!IsOwner) return;

            _mainMission = mission;
            ShareMissions();
            OnMainMissionReceived?.Invoke(mission);
            AnnounceReceived();
        }

        public void CompleteMainMission()
        {
            if (!IsOwner) return;
            if (_mainMission == null) return;
            
            MissionSO completed = _mainMission;
            _mainMission = null;
            ShareMissions();
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
            ShareMissions();
        }
        
        public bool HasPersonalMission(MissionSO mission)
        {
            return _personalMissions.Contains(mission);
        }
    } 
}

