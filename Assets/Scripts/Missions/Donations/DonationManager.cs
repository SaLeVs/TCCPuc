using System;
using System.Collections.Generic;
using Components.Sound;
using Enums;
using Player;
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
        [SerializeField] private float minEvaluationInterval = 3f;
        [SerializeField] private float maxEvaluationInterval = 10f;

        [Tooltip("Quiet period after any donation before the pool may be rolled again. This is " +
                 "what stops two donations landing seconds apart — the per-type cooldown only " +
                 "ever spaced a type from itself, never one type from another.")]
        [SerializeField, Min(0f)] private float minSecondsBetweenDonations = 90f;

        [Header("World sounds")]
        [Tooltip("Played at the recipient when a donation reaches them, for everyone nearby to hear.")]
        [SerializeField] private SoundDefinitionSO receivedSound;

        [Tooltip("Played at the recipient when they complete their donation.")]
        [SerializeField] private SoundDefinitionSO completedSound;

        public NetworkList<DonationNetworkState> NetworkStates => _networkStates;
        
        private readonly NetworkList<DonationNetworkState> _networkStates = new();
        private readonly Dictionary<string, DonationInstance> _activeInstances = new();
        private readonly Dictionary<string, float> _cooldownTimers = new();
        private readonly List<string> _cooldownKeys = new();
        private readonly Dictionary<string, DonationDefinition> _definitionsById = new();

        private readonly Dictionary<RecordableTarget, HashSet<ulong>> _recordingWatchers = new();
        private readonly List<PlayerState> _recipientCandidates = new();
        private readonly List<DonationDefinition> _rollOrder = new();

        private readonly NetworkVariable<int> _manualViewerCount = new(0);
        private float _currentEvaluationInterval;
        private float _evaluationTimer;
        private float _timeSinceLastSpawn = float.MaxValue;

        /// <summary>Seconds left in the quiet period. 0 when the pool may be rolled.</summary>
        public float QuietSecondsRemaining =>
            Mathf.Max(0f, minSecondsBetweenDonations - _timeSinceLastSpawn);
        
        public int ViewerCount => _manualViewerCount.Value > 0 ? _manualViewerCount.Value : (NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0);

        private void Awake()
        {
            Instance = this;
            BuildDefinitionLookup();
        }

        /// <summary>
        /// donationId -> definition, built on every peer: the pool is serialized in the prefab, so
        /// clients hold the same assets the server does. The network state only carries the id, so
        /// this is what lets the UI reach a donation's authored visuals (icon) on the client side.
        /// </summary>
        private void BuildDefinitionLookup()
        {
            _definitionsById.Clear();
            if (donationPool == null) return;

            foreach (var definition in donationPool)
            {
                if (definition == null || string.IsNullOrEmpty(definition.donationId)) continue;

                _definitionsById[definition.donationId] = definition;
            }
        }

        /// <summary>Definition behind a donation id, or null when the id isn't in the pool.</summary>
        public DonationDefinition GetDefinition(string donationId)
        {
            if (string.IsNullOrEmpty(donationId)) return null;

            return _definitionsById.TryGetValue(donationId, out var definition) ? definition : null;
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

            if (_timeSinceLastSpawn < float.MaxValue) _timeSinceLastSpawn += Time.deltaTime;

            _evaluationTimer += Time.deltaTime;

            if (_evaluationTimer >= _currentEvaluationInterval)
            {
                _evaluationTimer = 0f;
                RollNextEvaluationInterval();

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

            // Reused: this runs every frame for as long as any type is cooling down, and a fresh
            // list per frame was garbage the collector had to come back for.
            _cooldownKeys.Clear();
            _cooldownKeys.AddRange(_cooldownTimers.Keys);

            foreach (var key in _cooldownKeys)
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
                // A recipient who died, escaped or left can never finish it, and while it stays
                // active that donation is closed to everyone else.
                if (instance.State == DonationState.Active && (instance.IsExpired(now) || !CanStillComplete(instance.RecipientClientId)))
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

        /// <summary>
        /// One pass over the pool, landing at most one donation: two players never get one at the
        /// same moment, and the quiet period that follows keeps the next one well apart. The pool
        /// is walked in a fresh random order so the first definition in it is not favoured — each
        /// still rolls its own chance, so the audience tuning is untouched.
        /// </summary>
        private void EvaluateSpawns()
        {
            if (donationPool == null) return;

            if (_timeSinceLastSpawn < minSecondsBetweenDonations) return;

            // Nobody free to receive one: every player still in the match is already busy with theirs.
            PlayerState recipient = PickRecipient();
            if (recipient == null) return;

            int viewers = ViewerCount;

            _rollOrder.Clear();
            _rollOrder.AddRange(donationPool);
            Shuffle(_rollOrder);

            foreach (var definition in _rollOrder)
            {
                if (definition == null || string.IsNullOrEmpty(definition.donationId)) continue;

                if (_cooldownTimers.TryGetValue(definition.donationId, out float remaining) && remaining > 0f)
                    continue;

                // A donation is one person's: the same one is never running for two players at once.
                if (HasActiveInstanceOf(definition.donationId)) continue;

                float chance = definition.triggerRule.EvaluateChance(viewers);

                // Strictly less-than: Random.value can return exactly 0, so `<=` would let a
                // chance of 0 through — which now happens on purpose whenever the audience is
                // below a rule's minViewersRequired.
                if (UnityEngine.Random.value >= chance) continue;

                SpawnDonation(definition, recipient);
                _timeSinceLastSpawn = 0f;
                return;
            }
        }

        private static void Shuffle(List<DonationDefinition> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
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

        private static bool CanStillComplete(ulong clientId)
        {
            NetworkObject playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject == null || !playerObject.TryGetComponent(out PlayerState player)) return false;

            return !player.IsDead && !player.HasWon && !player.HasEscapedServerSide;
        }

        private bool HasActiveDonation(ulong clientId)
        {
            foreach (var instance in _activeInstances.Values)
            {
                if (instance.State == DonationState.Active && instance.RecipientClientId == clientId) return true;
            }
            return false;
        }

        private void SpawnDonation(DonationDefinition definition, PlayerState recipient)
        {
            _cooldownTimers[definition.donationId] = definition.triggerRule.cooldownSeconds;

            double now = NetworkManager.Singleton.ServerTime.TimeAsFloat;
            double expireTime = definition.durationSeconds > 0f ? now + definition.durationSeconds : 0.0;

            DonationInstance instance = new DonationInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                Definition = definition,
                DonorName = PickDonorName(definition),
                Amount = UnityEngine.Random.Range(definition.minAmountMoney, definition.maxAmountMoney),
                SpawnTime = now,
                ExpireTime = expireTime,
                RecipientClientId = recipient.OwnerClientId,
                RecipientName = recipient.PlayerInfos.PlayerName.Value.ToString(),
                State = DonationState.Active,
                Progress = 0f
            };

            _activeInstances[instance.InstanceId] = instance;
            PushNetworkState(instance);
            PlayAtRecipient(receivedSound, instance);
            OnDonationSpawned?.Invoke(instance);
        }

        /// <summary>
        /// Draws who the next donation goes to, uniformly among the players still in the match — not
        /// dead, not escaped — who are not already working on one. Null when nobody qualifies: the
        /// donation simply waits for the next evaluation instead of doubling up on someone.
        /// </summary>
        private PlayerState PickRecipient()
        {
            _recipientCandidates.Clear();

            NetworkManager network = NetworkManager.Singleton;
            if (network == null) return null;

            foreach (ulong clientId in network.ConnectedClientsIds)
            {
                NetworkObject playerObject = network.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObject == null || !playerObject.TryGetComponent(out PlayerState player)) continue;

                if (player.IsDead || player.HasWon || player.HasEscapedServerSide) continue;
                if (HasActiveDonation(clientId)) continue;

                _recipientCandidates.Add(player);
            }

            if (_recipientCandidates.Count == 0) return null;

            return _recipientCandidates[UnityEngine.Random.Range(0, _recipientCandidates.Count)];
        }

        /// <summary>
        /// Server-side, once: a world sound at the recipient, following them while it plays, so the
        /// players around them hear it from where they are.
        /// </summary>
        private void PlayAtRecipient(SoundDefinitionSO sound, DonationInstance instance)
        {
            if (sound == null) return;

            NetworkObject playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(instance.RecipientClientId);
            if (playerObject == null) return;

            WorldSound.Play(sound, playerObject.transform.position, playerObject);
        }

        private string PickDonorName(DonationDefinition definition)
        {
            if (definition.fakeDonorNames == null) return "Anonymous";

            // Drawn from the same population the chat speaks with, so a donor is someone
            // the player has seen in chat rather than a name out of a separate list.
            return definition.fakeDonorNames.TryPickName(out string name) ? name : "Anonymous";
        }

        /// <summary>Call this from your recording progress detection system (see DonationRecordableZone).</summary>
        public void ReportRecordingProgress(ulong clientId, RecordableTarget target, float deltaSeconds)
        {
            if (!IsServer) return;
            
            List<DonationInstance> completedNow = null;
            
            foreach (var instance in _activeInstances.Values)
            {
                if (instance.State != DonationState.Active) continue;
                if (instance.RecipientClientId != clientId) continue;
                if (instance.Definition.category != DonationCategory.Recording) continue;
                if (instance.Definition.targetType != target) continue;

                float step = instance.Definition.requiredRecordingSeconds > 0f
                    ? deltaSeconds / instance.Definition.requiredRecordingSeconds
                    : 1f;

                AdvanceProgress(instance, step);

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
                if (instance.RecipientClientId != clientId) continue;
                if (instance.Definition.category != DonationCategory.MicSpeech) continue;
                if (instance.Definition.micActionId != micActionId) continue;

                if(instance.Definition.targetType != RecordableTarget.None)
                {
                    bool isWatchingTarget = _recordingWatchers.TryGetValue(instance.Definition.targetType, out var watchers)
                                                                    && watchers.Contains(clientId);
                    if (!isWatchingTarget) continue;
                }
                
                float step = instance.Definition.requiredSpeechSeconds > 0f ? deltaSeconds / instance.Definition.requiredSpeechSeconds : 1f;

                AdvanceProgress(instance, step);

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
            PlayAtRecipient(completedSound, instance);
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

        /// <summary>
        /// Moves a donation along and replicates it once per whole percent rather than every frame.
        /// </summary>
        /// <remarks>
        /// Progress is fed a frame at a time while a player records or talks, and every push rewrites
        /// the whole entry in the NetworkList - donor name and message included - to every client.
        /// Sixty full entries a second for a bar that moves a pixel at a time was bandwidth and
        /// client-side UI work for nothing anyone could see.
        /// </remarks>
        private void AdvanceProgress(DonationInstance instance, float step)
        {
            float before = instance.Progress;
            instance.Progress = Mathf.Clamp01(before + step);

            bool crossedPercent = Mathf.FloorToInt(instance.Progress * 100f) != Mathf.FloorToInt(before * 100f);
            if (crossedPercent || instance.Progress >= 1f) PushNetworkState(instance);
        }

        private void PushNetworkState(DonationInstance instance)
        {
            DonationNetworkState state = new DonationNetworkState
            {
                InstanceId = instance.InstanceId,
                DonationId = instance.Definition.donationId,
                DonorName = instance.DonorName,
                Message = instance.Definition.message,
                RecipientClientId = instance.RecipientClientId,
                RecipientName = instance.RecipientName,
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
