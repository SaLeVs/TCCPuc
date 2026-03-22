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
    }
}