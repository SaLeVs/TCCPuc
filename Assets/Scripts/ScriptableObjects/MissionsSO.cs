using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Mission", menuName = "ScriptableObjects/Game/Mission")]
    public class MissionsSO : ScriptableObject
    {
        public string missionName;
    
        public List<RoomDataSO> requiredRooms;
        public List<RoomDataSO> baseRooms;
        public List<RoomDataSO> lootRooms;
        
        
    }
}


