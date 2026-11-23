using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Contract", menuName = "ScriptableObjects/Game/Contract")]
    public class ContractsSO : ScriptableObject
    {
        public string contractName;

        public GameObject mainMissionPrefab;
        public MissionSO mainMission;

        public List<MissionSO> personalMissions;

        public List<RoomDataSO> baseRooms;
        public List<RoomDataSO> lootRooms;

        public float maxAudience;

        [Header("Ready board")]
        [Tooltip("Line under the contract name on the lobby board (the play's poster title).")]
        public string posterTitle;

        [Tooltip("What the players read on the lobby board before accepting the contract.")]
        [TextArea(3, 8)] public string description;

        [Tooltip("Poster shown on the lobby board. Leave empty to show only the texts.")]
        public Sprite poster;

        [Tooltip("1 = easy, 2 = medium, 3 = hard. Shown as pips on the lobby board.")]
        [Range(1, 3)] public int difficulty = 1;

        public List<RoomDataSO> GetAllRequiredRooms()
        {
            List<RoomDataSO> allRequiredRooms = new List<RoomDataSO>();

            foreach (MissionSO mission in personalMissions)
            {
                if (mission.requiredRoom != null)
                {
                    allRequiredRooms.Add(mission.requiredRoom);
                }
            }

            return allRequiredRooms;
        }

        public MissionSO GetMissionByID(int id)
        {
            return personalMissions.Find(missionSo => missionSo.missionID == id);
        }

    }
}
