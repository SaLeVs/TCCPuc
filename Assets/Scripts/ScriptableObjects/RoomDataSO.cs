using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Room", menuName = "ScriptableObjects/Game/RoomData")]
    public class RoomDataSO : ScriptableObject
    {
        public int roomID;
        
        public GameObject prefab;
        public RoomType roomType;

        public bool isUniqueRoom;
        public List<NetworkSpawnEntry> networkSpawnEntries;
    }
    
    [System.Serializable]
    public class NetworkSpawnEntry
    {
        public GameObject prefab;
        public Vector3 localOffset;
        public Vector3 localRotation;
    }
}