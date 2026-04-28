using Enums;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Mission", menuName = "ScriptableObjects/Game/Mission")]
    public class MissionSO : ScriptableObject
    {
        public int[] requiredRoomIDs;
        
        public MissionObjectiveType objectiveType;
        
        public string missionName;
        public string instructions;
    }
    
}