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
        [SerializeField] private GameObject chatUi;

        [Header("Timing")]
        [SerializeField] private float minDelay = 0.5f;
        [SerializeField] private float maxDelay = 2.5f;

        
        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name == nameof(Scenes.Game))
            {
                visionSensor.OnTargetEnterStatic += VisionSensor_OnTargetSeen;
                visionSensor.OnTargetEnter += VisionSensor_OnNetworkTargetSeen;
                chatUi.SetActive(true);
                Debug.Log("ChatManager: Vision sensor enabled");
            }
            else
            {
                chatUi.SetActive(false);
            }
        }

        private void VisionSensor_OnNetworkTargetSeen(GameObject target)
        {
            if (!target.TryGetComponent(out RecordableIdentifier identifier)) return;
            
            TriggerMessage(identifier.targetType);
        }

        private void VisionSensor_OnTargetSeen(RecordableTarget target)
        {
            TriggerMessage(target);
        }

        private void TriggerMessage(RecordableTarget target)
        {
            if (!messageDatabase.TryGetData(target, out TargetChatData entry))
            {
                Debug.Log($"ChatManager: No chat data for {target}, skipping.");
                return;
            }

            string message = entry.GetWeightedRandom();
            string viewer = nameDatabase.GetNext();
            float delay = UnityEngine.Random.Range(minDelay, maxDelay);

            StartCoroutine(SendDelayed(viewer, message, delay));
        }

        private IEnumerator SendDelayed(string viewer, string message, float delay)
        {
            yield return new WaitForSeconds(delay);
            OnMessageSent?.Invoke(viewer, message);
            Debug.Log($"ChatManager: Message '{message}' sent to '{viewer}'");
        }
        
        
        private void OnDisable()
        {
            StopAllCoroutines();

            if (SceneManager.GetActiveScene().name == nameof(Scenes.Game))
            {
                visionSensor.OnTargetEnterStatic -= VisionSensor_OnTargetSeen;
                visionSensor.OnTargetEnter -= VisionSensor_OnNetworkTargetSeen;
            }

            chatUi.SetActive(false);
        }
        
    }
}

