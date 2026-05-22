using System;
using System.Collections;
using Chat;
using Components;
using Enums;
using UnityEngine;
using ScriptableObjects;
using UnityEngine.SceneManagement;

namespace Player.Chat
{
    public class ChatManager : MonoBehaviour
    {
        public event Action<string, string> OnMessageSent;
        
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private ChatMessageDatabaseSO messageDatabase;
        [SerializeField] private ViewerNameDatabaseSO nameDatabase;

        [Header("Timing")]
        [SerializeField] private float minDelay = 0.5f;
        [SerializeField] private float maxDelay = 2.5f;

        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name == nameof(Scenes.Gameplay))
            {
                visionSensor.OnTargetEnter += OnTargetSeen;
            }
        }

        private void OnTargetSeen(GameObject target)
        {
            if (!target.TryGetComponent(out RecordableIdentifier identifier)) return;
            if (!messageDatabase.TryGetData(identifier.targetType, out TargetChatData entry)) return;

            string message = entry.GetWeightedRandom();
            string viewer = nameDatabase.GetNext();
            float  delay = UnityEngine.Random.Range(minDelay, maxDelay);

            StartCoroutine(SendDelayed(viewer, message, delay));
        }

        private IEnumerator SendDelayed(string viewer, string message, float delay)
        {
            yield return new WaitForSeconds(delay);
            OnMessageSent?.Invoke(viewer, message);
        }
        
        private void OnDisable()
        {
            if (SceneManager.GetActiveScene().name == nameof(Scenes.Gameplay))
            {
                visionSensor.OnTargetEnter -= OnTargetSeen;
            }
        }
    }
}

