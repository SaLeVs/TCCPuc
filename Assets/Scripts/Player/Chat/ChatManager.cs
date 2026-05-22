using Components;
using UnityEngine;
using ScriptableObjects;

namespace Player.Chat
{
    public class ChatManager : MonoBehaviour
    {
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private ChatMessageDatabaseSO messageDatabase;
        [SerializeField] private ViewerNameDatabaseSO nameDatabaseSo;
        
    }
    
}

