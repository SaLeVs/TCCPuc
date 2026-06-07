using System.Collections.Generic;
using Missions;
using ScriptableObjects;
using UnityEngine;

namespace UI
{
    public class PlayerMissionHolderUi : MonoBehaviour
    {
        [SerializeField] private PlayerMissionHolder missionHolder;
        [SerializeField] private MissioninstructionsUI missionInstructionsPrefab;
        [SerializeField] private Transform content;

        private readonly Dictionary<MissionSO, MissioninstructionsUI> _spawnedMissions = new();

    
        private void Start()
        {
            missionHolder.OnPersonalMissionReceived += SpawnMissionItem;
            missionHolder.OnPersonalMissionCompleted += RemoveMissionItem;
            missionHolder.OnMainMissionReceived += SpawnMissionItem;
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
            missionHolder.OnPersonalMissionReceived -= SpawnMissionItem;
            missionHolder.OnPersonalMissionCompleted -= RemoveMissionItem;
            missionHolder.OnMainMissionReceived -= SpawnMissionItem;
        }
    
    }
}
