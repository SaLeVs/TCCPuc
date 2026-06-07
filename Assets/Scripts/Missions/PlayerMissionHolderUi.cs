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

        private PlayerMissionHolder _missionHolder;
        private readonly Dictionary<MissionSO, MissioninstructionsUI> _spawnedMissions = new();

        public void Initialize(PlayerMissionHolder holder)
        {
            _missionHolder = holder;
            _missionHolder.OnPersonalMissionReceived += SpawnMissionItem;
            _missionHolder.OnPersonalMissionCompleted += RemoveMissionItem;
            _missionHolder.OnMainMissionReceived += SpawnMissionItem;
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

        private void OnDestroy()
        {
            if (_missionHolder == null) return;
            _missionHolder.OnPersonalMissionReceived -= SpawnMissionItem;
            _missionHolder.OnPersonalMissionCompleted -= RemoveMissionItem;
            _missionHolder.OnMainMissionReceived -= SpawnMissionItem;
        }
    }
}