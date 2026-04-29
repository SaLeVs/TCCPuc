using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Contract", menuName = "ScriptableObjects/Game/Contract")]
    public class ContractsSO : ScriptableObject
    {
        public string contractName;
    
        public MissionSO mainMission;
        public List<MissionSO> personalMissions;
        
        public List<RoomDataSO> requiredRooms;
        public List<RoomDataSO> baseRooms;
        public List<RoomDataSO> lootRooms;
        
        
        public List<RoomDataSO> GetAllRequiredRooms()
        {
            List<RoomDataSO> allRequiredRooms = new List<RoomDataSO>();

            if (mainMission.requiredRoom != null)
            {
                allRequiredRooms.Add(mainMission.requiredRoom);
            }

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


