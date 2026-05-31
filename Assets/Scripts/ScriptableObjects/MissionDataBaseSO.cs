using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "Mission data base", menuName = "ScriptableObjects/Game/MissionDataBase")]
    public class MissionDataBaseSO : ScriptableObject
    {
        [SerializeField] private List<MissionSO> missions = new();

        public int GetId(MissionSO mission) => missions.IndexOf(mission);

        public MissionSO GetMission(int id)
        {
            if (id < 0 || id >= missions.Count) return null;
            return missions[id];
        }
    }
}
