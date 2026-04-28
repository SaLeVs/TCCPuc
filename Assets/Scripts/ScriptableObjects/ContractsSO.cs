using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Contract", menuName = "ScriptableObjects/Game/Contract")]
    public class ContractsSO : ScriptableObject
    {
        public string contractName;
    
        public List<RoomDataSO> requiredRooms;
        public List<RoomDataSO> baseRooms;
        public List<RoomDataSO> lootRooms;
        
        
    }
}


