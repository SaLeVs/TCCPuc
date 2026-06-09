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
        
        
        public void Initialize(PlayerMissionHolder holder)
        {
            _missionHolder = holder;
            _missionHolder.OnPersonalMissionReceived += SpawnMissionItem;
            _missionHolder.OnPersonalMissionCompleted += RemoveMissionItem;
            _missionHolder.OnMainMissionReceived += SpawnMissionItem;
            _missionHolder.OnMainMissionCompleted += RemoveMissionItem;
            _missionHolder.OnMessageReceived += ShowMessage;
        }

        private void SpawnMissionItem(MissionSO mission)
        {
            if (_spawnedMissions.ContainsKey(mission)) return;

            MissioninstructionsUI item = Instantiate(missionInstructionsPrefab, content);
            item.Setup(mission);
            _spawnedMissions[mission] = item;
        }

        private void RemoveMissionItem(MissionSO mission)
        {
            if (!_spawnedMissions.Remove(mission, out MissioninstructionsUI missionUi)) return;
            Destroy(missionUi.gameObject);
        }

        private void ShowMessage(string message)
        {
            if (_currentMessage != null)
            {
                Destroy(_currentMessage.gameObject);
            }

            _currentMessage = Instantiate(messagePrefab, content);
            _currentMessage.Setup(message);
        }

        
        private void OnDestroy()
        {
            if (_missionHolder == null) return;
            _missionHolder.OnPersonalMissionReceived -= SpawnMissionItem;
            _missionHolder.OnPersonalMissionCompleted -= RemoveMissionItem;
            _missionHolder.OnMainMissionReceived -= SpawnMissionItem;
            _missionHolder.OnMessageReceived -= ShowMessage;
            _missionHolder.OnMainMissionCompleted -= RemoveMissionItem;
        }
        
    }
}