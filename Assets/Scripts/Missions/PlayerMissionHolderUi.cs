using System.Collections.Generic;
using Missions;
using ScriptableObjects;
using UnityEngine;

namespace Missions
{
    public class PlayerMissionHolderUi : MonoBehaviour
    {
        [SerializeField] private MissioninstructionsUI missionInstructionsPrefab;
        [SerializeField] private Transform content;
        [SerializeField] private MissionMessageUi messagePrefab;

        private PlayerMissionHolder _missionHolder;
        private readonly Dictionary<MissionSO, MissioninstructionsUI> _spawnedMissions = new();
        private MissionMessageUi _currentMessage;

        // Someone else's missions while spectating them; null shows this player's own.
        private PlayerMissionHolder _watched;
        private readonly List<MissionSO> _sharedBuffer = new();
        
        
        public void Initialize(PlayerMissionHolder holder)
        {
            _missionHolder = holder;
            _missionHolder.OnPersonalMissionReceived += SpawnMissionItem;
            _missionHolder.OnPersonalMissionCompleted += RemoveMissionItem;
            _missionHolder.OnMainMissionReceived += SpawnMissionItem;
            _missionHolder.OnMainMissionCompleted += RemoveMissionItem;
            _missionHolder.OnMessageReceived += ShowMessage;
        }

        /// <summary>
        /// Shows <paramref name="holder"/>'s missions instead of this player's own - a spectator sees
        /// what the player they watch has to do, kept up to date as it changes. Null, or this
        /// player's own holder, goes back to the own list.
        /// </summary>
        public void Watch(PlayerMissionHolder holder)
        {
            if (holder == _missionHolder) holder = null;
            if (holder == _watched) return;

            if (_watched != null) _watched.OnSharedMissionsChanged -= RebuildWatched;

            _watched = holder;
            ClearItems();
            ClearMessage();

            if (_watched != null)
            {
                _watched.OnSharedMissionsChanged += RebuildWatched;
                RebuildWatched();
                return;
            }

            if (_missionHolder == null) return;

            if (_missionHolder.MainMission != null) SpawnItem(_missionHolder.MainMission);
            foreach (MissionSO mission in _missionHolder.PersonalMissions) SpawnItem(mission);
        }

        private void RebuildWatched()
        {
            if (_watched == null) return;

            ClearItems();

            _watched.GetSharedMissions(_sharedBuffer);
            foreach (MissionSO mission in _sharedBuffer) SpawnItem(mission);
        }

        private void SpawnMissionItem(MissionSO mission)
        {
            // Spectating someone: this player's own list comes back when the spectating ends.
            if (_watched != null) return;

            SpawnItem(mission);
        }

        private void SpawnItem(MissionSO mission)
        {
            if (mission == null || _spawnedMissions.ContainsKey(mission)) return;

            MissioninstructionsUI item = Instantiate(missionInstructionsPrefab, content);
            item.Setup(mission);
            _spawnedMissions[mission] = item;
        }

        private void RemoveMissionItem(MissionSO mission)
        {
            if (_watched != null) return;
            if (!_spawnedMissions.Remove(mission, out MissioninstructionsUI missionUi)) return;
            Destroy(missionUi.gameObject);
        }

        private void ClearItems()
        {
            foreach (MissioninstructionsUI item in _spawnedMissions.Values)
            {
                if (item != null) Destroy(item.gameObject);
            }

            _spawnedMissions.Clear();
        }

        private void ShowMessage(string message)
        {
            if (_watched != null) return;

            ClearMessage();

            _currentMessage = Instantiate(messagePrefab, content);
            _currentMessage.Setup(message);
        }

        private void ClearMessage()
        {
            if (_currentMessage != null)
            {
                Destroy(_currentMessage.gameObject);
            }

            _currentMessage = null;
        }

        
        private void OnDestroy()
        {
            if (_watched != null) _watched.OnSharedMissionsChanged -= RebuildWatched;

            if (_missionHolder == null) return;
            _missionHolder.OnPersonalMissionReceived -= SpawnMissionItem;
            _missionHolder.OnPersonalMissionCompleted -= RemoveMissionItem;
            _missionHolder.OnMainMissionReceived -= SpawnMissionItem;
            _missionHolder.OnMessageReceived -= ShowMessage;
            _missionHolder.OnMainMissionCompleted -= RemoveMissionItem;
        }
        
    }
}
