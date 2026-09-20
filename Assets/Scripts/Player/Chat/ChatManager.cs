using System;
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
    /// <summary>
    /// Turns what happens around the local player into things the chat reacts to, and hands them to
    /// the <see cref="ChatDirector"/> to be paced out.
    ///
    /// <para>Three sources feed it: the vision sensor (the streamer looked at something), the
    /// replicated health of every player (someone got hurt or died), and the neutral
    /// <see cref="ChatStimulusBus"/>, which anything anywhere can raise a topic on with one static
    /// call - donations, missions, the audience, a locked door, a tutorial step.</para>
    ///
    /// <para>Runs on the owning client only, by construction: the vision sensor RPCs target the
    /// owner, and nothing here writes state anyone else can see.</para>
    /// </summary>
    public class ChatManager : MonoBehaviour
    {
        /// <summary>Unchanged on purpose so <see cref="ChatUi"/> keeps working as-is.</summary>
        public event Action<string, string> OnMessageSent;

        [Header("References")]
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private GameObject chatUi;

        [Header("Databases")]
        [SerializeField] private ChatMessageDatabaseSO messageDatabase;
        [SerializeField] private ChatTopicDatabaseSO topicDatabase;
        [SerializeField] private ChatAmbientDatabaseSO ambientDatabase;
        [SerializeField] private ViewerPopulationSO viewerPopulation;

        [Header("Reaction settings")]
        [SerializeField, Min(0)] private int minMessages = 1;
        [SerializeField, Min(0)] private int maxMessages = 3;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How much chat cares about the streamer looking at an normal thing")]
        private float normalIntensity = 0.25f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How much chat cares about the monster being on screen")]
        private float monsterIntensity = 0.8f;

        [SerializeField, Min(0f)]
        [Tooltip("How long half-finished viewing progress on a target is remembered after looking away")]
        private float pendingViewMemory = 30f;

        [Header("Health reactions")]
        [SerializeField, Min(0f)]
        [Tooltip("Damage in one hit, as a fraction of max health, before chat says anything")]
        private float hurtThreshold = 0.08f;

        [Header("Exploration hint")]
        [SerializeField, Min(1f)]
        [Tooltip("Seconds before a target counts as worth noticing again")]
        private float noveltyWindow = 120f;

        [SerializeField]
        [Tooltip("Nudges the player to go look at something new. Reset every time chat reacts to a target it has not covered recently")]
        private ChatNudge explorationNudge = new(ChatTopics.HintExplorationIdle, 75f, 50f, 0.3f, 0.65f);

        [SerializeField] private ChatDirector director = new();

        private readonly Dictionary<RecordableIdentifier, float> _activeTargets = new();
        private readonly Dictionary<RecordableIdentifier, PendingView> _pendingViewTime = new();
        private readonly Dictionary<RecordableIdentifier, float> _lastSentTime = new();
        private readonly Dictionary<string, float> _lastTopicTime = new();

        /// <summary>Ids raised with no row in the database. Warned about once each, not every time.</summary>
        private readonly HashSet<string> _warnedUnknownTopics = new();

        /// <summary>Reused every frame. Allocating this inside Update was pure GC churn.</summary>
        private readonly List<RecordableIdentifier> _tickBuffer = new();

        private bool _subscribed;

        private readonly struct PendingView
        {
            public readonly float accumulated;
            public readonly float stamp;

            public PendingView(float accumulated, float stamp)
            {
                this.accumulated = accumulated;
                this.stamp = stamp;
            }
        }


        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game))
            {
                chatUi.SetActive(false);
                return;
            }

            director.Initialize(viewerPopulation, ambientDatabase);
            director.OnLine += Director_OnLine;

            visionSensor.OnTargetEnter += VisionSensor_OnNetworkTargetSeen;
            visionSensor.OnTargetExit += VisionSensor_OnNetworkTargetExitSeen;

            visionSensor.OnTargetEnterStatic += VisionSensor_OnStaticTargetSeen;
            visionSensor.OnTargetExitStatic += VisionSensor_OnStaticTargetExitSeen;

            Health.OnAnyHealthChanged += Health_OnAnyHealthChanged;
            ChatStimulusBus.OnStimulus += ChatStimulusBus_OnStimulus;

            _subscribed = true;

            explorationNudge.ReportProgress();

            WarnAboutEmptyPools();

            chatUi.SetActive(true);
        }

        /// <summary>
        /// Says once, at startup, what has been wired up but never written for. Previously a missing
        /// pool surfaced as a fresh warning every time the player looked at it, and an entry that
        /// existed with an empty list surfaced as nothing at all.
        /// </summary>
        private void WarnAboutEmptyPools()
        {
            if (messageDatabase == null)
            {
                Debug.LogWarning($"{nameof(ChatManager)}: no message database assigned, sightings will produce no chat.", this);
            }
            else
            {
                List<RecordableTarget> missing = new List<RecordableTarget>(messageDatabase.MissingTargets());

                if (missing.Count > 0)
                {
                    string names = string.Join(", ", missing);

                    Debug.LogWarning($"{nameof(ChatManager)}: no chat lines for {names}", this);
                }
            }

            if (topicDatabase == null)
            {
                Debug.LogWarning($"{nameof(ChatManager)}: no topic database assigned, so nothing that happens in the match will produce chat.", this);
                return;
            }

            List<string> empty = new List<string>(topicDatabase.EmptyTopics());

            if (empty.Count == 0) return;

            Debug.LogWarning($"{nameof(ChatManager)}: these topics have no lines and will stay silent: {string.Join(", ", empty)}.", this);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            TickActiveTargets(deltaTime);

            explorationNudge.Tick(deltaTime);

            director.Tick(deltaTime, ChatStimulusBus.ViewerCount, ChatStimulusBus.AudienceDecaying);
        }

        private void Director_OnLine(string viewer, string message) => OnMessageSent?.Invoke(viewer, message);


        private void VisionSensor_OnNetworkTargetSeen(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier identifier))
            {
                HandleTargetEnter(identifier);
            }
            else
            {
                Debug.LogWarning($"[ChatManager] No RecordableIdentifier on {target.name}");
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

        /// <summary>
        /// Starts counting viewing time on a target.
        ///
        /// <para>The cooldown is deliberately not checked here anymore. It used to refuse entry,
        /// which meant a target inside its cooldown never started accumulating - so staring at the
        /// monster without blinking produced exactly one reaction and then nothing, no matter how
        /// long the cooldown had since expired. The cooldown now gates the firing instead.</para>
        /// </summary>
        private void HandleTargetEnter(RecordableIdentifier identifier)
        {
            if (!identifier.canBeReviewedForChat) return;

            if (_activeTargets.ContainsKey(identifier)) return;

            float resumedTime = 0f;

            if (_pendingViewTime.TryGetValue(identifier, out PendingView saved))
            {
                if (Time.time - saved.stamp <= pendingViewMemory)
                {
                    resumedTime = saved.accumulated;
                }

                _pendingViewTime.Remove(identifier);
            }

            _activeTargets[identifier] = resumedTime;
        }

        private void HandleTargetExit(RecordableIdentifier identifier)
        {
            if (!_activeTargets.TryGetValue(identifier, out float accumulated)) return;

            if (accumulated > 0f && accumulated < identifier.minimumViewTime)
            {
                _pendingViewTime[identifier] = new PendingView(accumulated, Time.time);
            }

            _activeTargets.Remove(identifier);
        }

        private void TickActiveTargets(float deltaTime)
        {
            if (_activeTargets.Count == 0) return;

            _tickBuffer.Clear();
            _tickBuffer.AddRange(_activeTargets.Keys);

            float now = Time.time;

            foreach (RecordableIdentifier identifier in _tickBuffer)
            {
                if (identifier == null)
                {
                    Forget(identifier);
                    continue;
                }

                float accumulated = _activeTargets[identifier] + deltaTime;

                if (accumulated < identifier.minimumViewTime)
                {
                    _activeTargets[identifier] = accumulated;
                    continue;
                }

                bool onCooldown = _lastSentTime.TryGetValue(identifier, out float lastSent) && now - lastSent < identifier.chatCooldown;

                if (onCooldown)
                {
                    // Ready, but still on cooldown. Hold at the threshold instead of piling up, so
                    // it fires once the moment the cooldown clears rather than several times over.
                    _activeTargets[identifier] = identifier.minimumViewTime;
                    continue;
                }

                // Something chat has not covered in a while counts as actually exploring. Checked
                // before the stamp below, which is what makes the window mean anything.
                if (!_lastSentTime.TryGetValue(identifier, out float covered) || now - covered > noveltyWindow)
                {
                    explorationNudge.ReportProgress();
                }

                TriggerSighting(identifier.targetType);

                _lastSentTime[identifier] = now;
                
                _activeTargets[identifier] = 0f;
                _pendingViewTime.Remove(identifier);
            }
        }


        private void Forget(RecordableIdentifier identifier)
        {
            _activeTargets.Remove(identifier);
            _pendingViewTime.Remove(identifier);
            _lastSentTime.Remove(identifier);
        }

        private void TriggerSighting(RecordableTarget target)
        {
            if (messageDatabase == null) return;
            if (!messageDatabase.TryGetData(target, out TargetChatData pool)) return;

            bool isMonster = target == RecordableTarget.Monster;

            director.Enqueue(pool, Random.Range(minMessages, Mathf.Max(minMessages, maxMessages) + 1), isMonster ? monsterIntensity : normalIntensity,
                isMonster ? ChatMood.Tense : ChatMood.Idle,
                subject: null,
                allowSpamWave: isMonster);
        }


        private void Health_OnAnyHealthChanged(Health health, float previous, float current)
        {
            if (health == null) return;

            bool isLocal = health.IsOwner;

            if (current <= 0f && previous > 0f)
            {
                Raise(isLocal ? ChatTopics.PlayerDied : ChatTopics.TeammateDied, 1f, ResolveName(health));
                return;
            }

            if (current >= previous) return;

            float max = Mathf.Max(1f, health.MaxHealth);

            if ((previous - current) / max < hurtThreshold) return;

            if (!isLocal) return;

            Raise(ChatTopics.PlayerHurt, 1f - Mathf.Clamp01(current / max), ResolveName(health));
        }

        /// <summary>
        /// The name this player chose, for lines using {subject}. Null when it cannot be resolved,
        /// which leaves the generic fallback in place.
        ///
        /// <para>Comes off <see cref="PlayerInfos"/>, which sits on the same object as the health:
        /// the server reads the name out of UserData on spawn and writes it into a NetworkVariable,
        /// so every client has every player's name, not just its own. That is what makes naming a
        /// dead teammate possible from here at all.</para>
        /// </summary>
        private static string ResolveName(Health health)
        {
            if (!health.TryGetComponent(out PlayerInfos infos)) return null;

            string playerName = infos.PlayerName.Value.ToString();

            return string.IsNullOrWhiteSpace(playerName) ? null : playerName;
        }

        private void ChatStimulusBus_OnStimulus(ChatStimulus stimulus) => Handle(stimulus);

        private void Raise(string topicId, float intensity, string subject = null)
        {
            Handle(new ChatStimulus(topicId, intensity, subject));
        }

        private void Handle(in ChatStimulus stimulus)
        {
            if (topicDatabase == null) return;

            if (!topicDatabase.TryGet(stimulus.TopicId, out ChatTopicEntry entry))
            {
                if (_warnedUnknownTopics.Add(stimulus.TopicId))
                {
                    Debug.LogWarning($"{nameof(ChatManager)}: nothing raised '{stimulus.TopicId}' in the topic database, so it said nothing.", this);
                }

                return;
            }

            if (entry.data == null || entry.data.Count == 0) return;

            if (entry.cooldown > 0f && Time.time - LastTimeFor(stimulus.TopicId) < entry.cooldown)
            {
                return;
            }

            _lastTopicTime[stimulus.TopicId] = Time.time;
            
            float intensity = Mathf.Clamp01(entry.intensity * stimulus.Intensity * 2f);

            director.Enqueue(entry.data, Random.Range(entry.minMessages, Mathf.Max(entry.minMessages, entry.maxMessages) + 1), intensity, 
                entry.mood,
                stimulus.Subject,
                entry.allowSpamWave,
                entry.priority,
                entry.ignoreViewerFloor);
        }

        private float LastTimeFor(string topicId)
        {
            return _lastTopicTime.GetValueOrDefault(topicId, float.NegativeInfinity);
        }


        private void OnDisable()
        {
            if (_subscribed)
            {
                visionSensor.OnTargetEnter -= VisionSensor_OnNetworkTargetSeen;
                visionSensor.OnTargetExit -= VisionSensor_OnNetworkTargetExitSeen;

                visionSensor.OnTargetEnterStatic -= VisionSensor_OnStaticTargetSeen;
                visionSensor.OnTargetExitStatic -= VisionSensor_OnStaticTargetExitSeen;

                Health.OnAnyHealthChanged -= Health_OnAnyHealthChanged;
                ChatStimulusBus.OnStimulus -= ChatStimulusBus_OnStimulus;

                director.OnLine -= Director_OnLine;

                _subscribed = false;
            }

            director.Clear();
            explorationNudge.Disarm();

            if (chatUi != null)
            {
                chatUi.SetActive(false);
            }

            _activeTargets.Clear();
            _pendingViewTime.Clear();
            _lastSentTime.Clear();
            _lastTopicTime.Clear();
            _warnedUnknownTopics.Clear();
            _tickBuffer.Clear();
        }
        
    }
}
