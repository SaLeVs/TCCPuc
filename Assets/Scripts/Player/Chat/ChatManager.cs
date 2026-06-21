using System;
using System.Collections;
using System.Collections.Generic;
using Chat;
using Components;
using Enums;
using UnityEngine;
using ScriptableObjects;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

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

        private readonly Dictionary<RecordableIdentifier, float> _activeTargets = new();
        private readonly Dictionary<RecordableIdentifier, float> _pendingViewTime = new();
        private readonly Dictionary<RecordableIdentifier, float> _lastSentTime = new();

        
        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game))
            {
                chatUi.SetActive(false);
                return;
            }

            visionSensor.OnTargetEnter += VisionSensor_OnNetworkTargetSeen;
            visionSensor.OnTargetExit += VisionSensor_OnNetworkTargetExitSeen;

            visionSensor.OnTargetEnterStatic += VisionSensor_OnStaticTargetSeen;
            visionSensor.OnTargetExitStatic += VisionSensor_OnStaticTargetExitSeen;

            chatUi.SetActive(true);
        }
        

        private void Update()
        {
            TickActiveTargets(Time.deltaTime);
        }

        private void VisionSensor_OnNetworkTargetSeen(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier identifier))
            {
                HandleTargetEnter(identifier);
            }
        }

        private void VisionSensor_OnNetworkTargetExitSeen(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier identifier))
            {
                HandleTargetExit(identifier);
            }
        }

        private void VisionSensor_OnStaticTargetSeen(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier identifier))
            {
                HandleTargetEnter(identifier);
            }
        }

        private void VisionSensor_OnStaticTargetExitSeen(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier identifier))
            {
                HandleTargetExit(identifier);
            }
        }

        private void HandleTargetEnter(RecordableIdentifier identifier)
        {
            if (!identifier.canBeReviewedForChat) return;

            if (_lastSentTime.TryGetValue(identifier, out float lastSent))
            {
                if (Time.time - lastSent < identifier.chatCooldown) return;
            }

            if (_activeTargets.ContainsKey(identifier)) return;

            float resumedTime = 0f;

            if (_pendingViewTime.TryGetValue(identifier, out float saved))
            {
                resumedTime = saved;
                _pendingViewTime.Remove(identifier);
            }

            _activeTargets[identifier] = resumedTime;
        }

        private void HandleTargetExit(RecordableIdentifier identifier)
        {
            if (!_activeTargets.TryGetValue(identifier, out float accumulated)) return;

            if (accumulated < identifier.minimumViewTime)
            {
                _pendingViewTime[identifier] = accumulated;
            }

            _activeTargets.Remove(identifier);
        }

        private void TickActiveTargets(float deltaTime)
        {
            List<RecordableIdentifier> targets = new List<RecordableIdentifier>(_activeTargets.Keys);

            foreach (RecordableIdentifier identifier in targets)
            {
                if (identifier == null)
                {
                    _activeTargets.Remove(identifier);
                    continue;
                }

                _activeTargets[identifier] += deltaTime;

                if (_activeTargets[identifier] < identifier.minimumViewTime) continue;

                TriggerMessage(identifier.targetType);

                _lastSentTime[identifier] = Time.time;

                _activeTargets.Remove(identifier);
                _pendingViewTime.Remove(identifier);
            }
        }

        private void TriggerMessage(RecordableTarget target)
        {
            if (!messageDatabase.TryGetData(target, out TargetChatData entry)) return;

            string viewer = nameDatabase.GetNext();
            string message = entry.GetWeightedRandom();

            StartCoroutine(SendDelayed(viewer, message, Random.Range(minDelay, maxDelay)));
        }

        private IEnumerator SendDelayed(string viewer, string message, float delay)
        {
            yield return new WaitForSeconds(delay);

            OnMessageSent?.Invoke(viewer, message);
        }

        
        private void OnDisable()
        {
            StopAllCoroutines();

            if (SceneManager.GetActiveScene().name == nameof(Scenes.Game))
            {
                visionSensor.OnTargetEnter -= VisionSensor_OnNetworkTargetSeen;
                visionSensor.OnTargetExit -= VisionSensor_OnNetworkTargetExitSeen;

                visionSensor.OnTargetEnterStatic -= VisionSensor_OnStaticTargetSeen;
                visionSensor.OnTargetExitStatic -= VisionSensor_OnStaticTargetExitSeen;
            }

            chatUi.SetActive(false);

            _activeTargets.Clear();
            _pendingViewTime.Clear();
        }
        
    }
}