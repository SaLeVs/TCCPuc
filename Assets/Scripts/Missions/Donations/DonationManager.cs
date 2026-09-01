using System;
using System.Collections.Generic;
using Enums;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Donations
{
    public class DonationManager : NetworkBehaviour
    {
        public event Action<DonationInstance> OnDonationSpawned;
        public event Action<DonationInstance> OnDonationCompleted;
        public event Action<DonationInstance> OnDonationExpired;
        
        public static DonationManager Instance { get; private set; }

        [Header("Possible donates")]
        [SerializeField] private DonationDefinition[] donationPool;

        [Header("Timing")]
        [Tooltip("Every how many seconds the server tries to roll new donations")]
        [Header("Timing")]
        [SerializeField] private float minEvaluationInterval = 3f;
        [SerializeField] private float maxEvaluationInterval = 10f;
        
        public NetworkList<DonationNetworkState> NetworkStates => _networkStates;
        
        private readonly NetworkList<DonationNetworkState> _networkStates = new();
        private readonly Dictionary<string, DonationInstance> _activeInstances = new();
        private readonly Dictionary<string, float> _cooldownTimers = new();

        private readonly Dictionary<RecordableTarget, HashSet<ulong>> _recordingWatchers = new();

        private readonly NetworkVariable<int> _manualViewerCount = new(0);
        private float _currentEvaluationInterval;
        private float _evaluationTimer;
        
        public int ViewerCount => _manualViewerCount.Value > 0 ? _manualViewerCount.Value : (NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0);

        private void Awake()
        {
            Instance = this;
        }
        
        private void Start()
        {
            if (!IsServer) return;

            RollNextEvaluationInterval();
        }

        private void RollNextEvaluationInterval()
        {
            _currentEvaluationInterval = UnityEngine.Random.Range(minEvaluationInterval, maxEvaluationInterval);
        }

        public void SetViewerCount(int value)
        {
            if (!IsServer) return;
            
            _manualViewerCount.Value = Mathf.Max(0, value);
        }

        private void Update()
        {
            if (!IsServer) return;

            TickCooldowns(Time.deltaTime);
            TickExpirations();
            TickRecordingWatchers(Time.deltaTime);

            _evaluationTimer += Time.deltaTime;

            if (_evaluationTimer >= _currentEvaluationInterval)
            {
                _evaluationTimer = 0f;

                EvaluateSpawns();
            }
        }

        /// <summary>Call this when a player starts "watching"/recording a RecordableTarget (e.g., from CameraVision).</summary>
        public void ReportTargetEnter(ulong clientId, RecordableTarget targetType)
        {
            if (!IsServer) return;

            if (!_recordingWatchers.TryGetValue(targetType, out var watchers))
            {
                watchers = new HashSet<ulong>();
                _recordingWatchers[targetType] = watchers;
            }

            watchers.Add(clientId);
        }

        /// <summary>Call this when a player stops "watching"/recording a RecordableTarget (e.g., from CameraVision).</summary>
        public void ReportTargetExit(ulong clientId, RecordableTarget targetType)
        {
            if (!IsServer) return;
            if (_recordingWatchers.TryGetValue(targetType, out var watchers))
            {
                watchers.Remove(clientId);
            }
        }

        private void TickRecordingWatchers(float delta)
        {
            if (_recordingWatchers.Count == 0) return;

            foreach (var kvp in _recordingWatchers)
            {
                if (kvp.Value.Count == 0) continue;

                foreach (var clientId in kvp.Value)
                {
                    ReportRecordingProgress(clientId, kvp.Key, delta);
                }
            }
        }

        private void TickCooldowns(float delta)
        {
            if (_cooldownTimers.Count == 0) return;

            var keys = new List<string>(_cooldownTimers.Keys);
            foreach (var key in keys)
            {
                _cooldownTimers[key] = Mathf.Max(0f, _cooldownTimers[key] - delta);
            }
        }

        private void TickExpirations()
        {
            if (_activeInstances.Count == 0) return;
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.IsListening) return;

            double now = NetworkManager.Singleton.ServerTime.TimeAsFloat;
            List<DonationInstance> toExpire = null;

            foreach (var instance in _activeInstances.Values)
            {
                if (instance.State == DonationState.Active && instance.IsExpired(now))
                {
                    (toExpire ??= new List<DonationInstance>()).Add(instance);
                }
            }

            if (toExpire == null) return;

            foreach (var instance in toExpire)
            {
                ExpireDonation(instance);
            }
        }

        private void EvaluateSpawns()
        {
            if (donationPool == null) return;

            foreach (var definition in donationPool)
            {
                if (definition == null || string.IsNullOrEmpty(definition.donationId)) continue;

                if (_cooldownTimers.TryGetValue(definition.donationId, out float remaining) && remaining > 0f)
                    continue;

                if (definition.stackingMode == DonationStackingMode.Exclusive && HasActiveInstanceOf(definition.donationId))
                    continue;

                float chance = definition.triggerRule.EvaluateChance(ViewerCount);
                if (UnityEngine.Random.value <= chance)
                {
                    SpawnDonation(definition);
                }
            }
        }

        private bool HasActiveInstanceOf(string donationId)
        {
            foreach (var instance in _activeInstances.Values)
            {
                if (instance.Definition.donationId == donationId) return true;
            }
            return false;
        }

        private void SpawnDonation(DonationDefinition definition)
        {
            RollNextEvaluationInterval();
            
            _cooldownTimers[definition.donationId] = definition.triggerRule.cooldownSeconds;

            double now = NetworkManager.Singleton.ServerTime.TimeAsFloat;
            double expireTime = definition.durationSeconds > 0f ? now + definition.durationSeconds : 0.0;

            DonationInstance instance = new DonationInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                Definition = definition,
                DonorName = PickDonorName(definition),
                Amount = UnityEngine.Random.Range(definition.minAmount, definition.maxAmount),
                SpawnTime = now,
                ExpireTime = expireTime,
                State = DonationState.Active,
                Progress = 0f
            };
            
            _activeInstances[instance.InstanceId] = instance;
            PushNetworkState(instance);
            OnDonationSpawned?.Invoke(instance);
        }

        private string PickDonorName(DonationDefinition definition)
        {
            if (definition.fakeDonorNames == null || definition.fakeDonorNames.viewerNames.Count == 0)
                return "Anonymous";

            return definition.fakeDonorNames.GetNext();
        }

        /// <summary>Call this from your recording progress detection system (see DonationRecordableZone).</summary>
        public void ReportRecordingProgress(ulong clientId, RecordableTarget target, float deltaSeconds)
        {
            if (!IsServer) return;
            
            List<DonationInstance> completedNow = null;
            
            foreach (var instance in _activeInstances.Values)
            {
                if (instance.State != DonationState.Active) continue;
                if (instance.Definition.category != DonationCategory.Recording) continue;
                if (instance.Definition.targetType != target) continue;

                float step = instance.Definition.requiredRecordingSeconds > 0f
                    ? deltaSeconds / instance.Definition.requiredRecordingSeconds
                    : 1f;

                instance.Progress = Mathf.Clamp01(instance.Progress + step);
                PushNetworkState(instance);

                if (instance.Progress >= 1f)
                {
                    (completedNow ??= new List<DonationInstance>()).Add(instance);
                }
            }

            if (completedNow != null)
            {
                foreach (var instance in completedNow)
                {
                    CompleteDonation(instance);
                }
            }
        }

        /// <summary>Call this from your microphone speech detection system (see DonationMicWatcher).</summary>
        public void ReportMicSpeech(ulong clientId, string micActionId, float deltaSeconds)
        {
            if (!IsServer) return;
            
            List<DonationInstance> completedNow = null;

            foreach (var instance in _activeInstances.Values)
            {
                if (instance.State != DonationState.Active) continue;
                if (instance.Definition.category != DonationCategory.MicSpeech) continue;
                if (instance.Definition.micActionId != micActionId) continue;

                if(instance.Definition.targetType != RecordableTarget.None)
                {
                    bool isWatchingTarget = _recordingWatchers.TryGetValue(instance.Definition.targetType, out var watchers)
                                                                    && watchers.Contains(clientId);
                    if (!isWatchingTarget) continue;
                }
                
                float step = instance.Definition.requiredSpeechSeconds > 0f ? deltaSeconds / instance.Definition.requiredSpeechSeconds : 1f;

                instance.Progress = Mathf.Clamp01(instance.Progress + step);
                PushNetworkState(instance);

                if (instance.Progress >= 1f)
                {
                    (completedNow ??= new List<DonationInstance>()).Add(instance);
                }
            }

            if (completedNow != null)
            {
                foreach (var instance in completedNow)
                {
                    CompleteDonation(instance);
                }
            }
        }

        private void CompleteDonation(DonationInstance instance)
        {
            instance.State = DonationState.Completed;
            PushNetworkState(instance);
            OnDonationCompleted?.Invoke(instance);
            _activeInstances.Remove(instance.InstanceId);
        }

        private void ExpireDonation(DonationInstance instance)
        {
            instance.State = DonationState.Expired;
            PushNetworkState(instance);
            OnDonationExpired?.Invoke(instance);
            _activeInstances.Remove(instance.InstanceId);
        }

        private void PushNetworkState(DonationInstance instance)
        {
            DonationNetworkState state = new DonationNetworkState
            {
                InstanceId = instance.InstanceId,
                DonationId = instance.Definition.donationId,
                DonorName = instance.DonorName,
                Message = instance.Definition.message,
                Amount = instance.Amount,
                Progress = instance.Progress,
                SpawnTime = instance.SpawnTime,
                ExpireTime = instance.ExpireTime,
                State = instance.State
            };

            for (int i = 0; i < _networkStates.Count; i++)
            {
                if (_networkStates[i].InstanceId == state.InstanceId)
                {
                    _networkStates[i] = state;
                    return;
                }
            }

            _networkStates.Add(state);
        }
    }
}