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
            List<RoomDataSO> rooms = new List<RoomDataSO>(requiredRooms);
            
            foreach (MissionSO mission in personalMissions)
            {
                foreach (int roomID in mission.requiredRoomIDs)
                {
                    RoomDataSO matchRoom = 
                        requiredRooms.Find(roomData => roomData.roomID == roomID) ?? baseRooms.Find(roomData => roomData.roomID == roomID);

                    if (matchRoom != null && !rooms.Contains(matchRoom))
                    {
                        rooms.Add(matchRoom);
                    }
                }
            }

            return rooms;
        }
        
        public MissionSO GetMissionByID(int id)
        {
            return personalMissions.Find(missionSo => missionSo.missionID == id);
        }
        
    }
}


