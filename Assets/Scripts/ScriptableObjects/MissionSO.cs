using Enums;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Mission", menuName = "ScriptableObjects/Game/Mission")]
    public class MissionSO : ScriptableObject
    {
        public int missionID;
        
        public string missionName;
        public string instructions;
        
        public MissionObjectiveType objectiveType;
        public RoomDataSO requiredRoom;
    }
}