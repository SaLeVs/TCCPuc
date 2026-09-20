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

        [Header("Sightings")]
        [SerializeField, Min(0)] private int minMessages = 1;
        [SerializeField, Min(0)] private int maxMessages = 3;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How much chat cares about the streamer looking at an ordinary thing.")]
        private float sightingIntensity = 0.25f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How much chat cares about the monster being on screen. Well above the ordinary " +
                 "sighting value - this is what turns the room tense.")]
        private float monsterSightingIntensity = 0.8f;

        [SerializeField, Min(0f)]
        [Tooltip("How long half-finished viewing progress on a target is remembered after looking " +
                 "away. Used to be forever, which let a player bank 1.9 of 2 seconds, leave for ten " +
                 "minutes and trigger the reaction with a glance.")]
        private float pendingViewMemory = 12f;

        [Header("Health reactions")]
        [SerializeField, Min(0f)]
        [Tooltip("Damage in one hit, as a fraction of max health, before chat says anything. Stops " +
                 "a damage-over-time zone from generating a reaction per tick.")]
        private float hurtThreshold = 0.08f;

        [Header("Exploration hint")]
        [SerializeField, Min(1f)]
        [Tooltip("Seconds before a target counts as worth noticing again. A target chat already " +
                 "talked about inside this window is the same scenery, not something new, so " +
                 "staring at it does not count as exploring.")]
        private float noveltyWindow = 120f;

        [SerializeField]
        [Tooltip("Nudges the player to go look at something new. Reset every time chat reacts to a " +
                 "target it has not covered recently.")]
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
            public readonly float Accumulated;
            public readonly float Stamp;

            public PendingView(float accumulated, float stamp)
            {
                Accumulated = accumulated;
                Stamp = stamp;
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
                Debug.LogWarning($"{nameof(ChatManager)}: no message database assigned, sightings " +
                                 "will produce no chat.", this);
            }
            else
            {
                List<RecordableTarget> missing = new List<RecordableTarget>(messageDatabase.MissingTargets());

                if (missing.Count > 0)
                {
                    string names = string.Join(", ", missing);

                    Debug.LogWarning($"{nameof(ChatManager)}: no chat lines for {names}. Objects " +
                                     "carrying those targets will be looked at in silence - which " +
                                     "only matters for the ones actually placed in the game.", this);
                }
            }

            if (topicDatabase == null)
            {
                Debug.LogWarning($"{nameof(ChatManager)}: no topic database assigned, so nothing " +
                                 "that happens in the match will produce chat.", this);
                return;
            }

            List<string> empty = new List<string>(topicDatabase.EmptyTopics());

            if (empty.Count == 0) return;

            Debug.LogWarning($"{nameof(ChatManager)}: these topics have no lines and will stay " +
                             $"silent: {string.Join(", ", empty)}.", this);
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
        /// <para>The cooldown is deliberately not checked here any more. It used to refuse entry,
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
                if (Time.time - saved.Stamp <= pendingViewMemory)
                {
                    resumedTime = saved.Accumulated;
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

                bool onCooldown = _lastSentTime.TryGetValue(identifier, out float lastSent) &&
                                  now - lastSent < identifier.chatCooldown;

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

                // Kept active rather than removed: the player is still looking, and if they keep
                // looking past the cooldown chat should pick the subject back up.
                _activeTargets[identifier] = 0f;
                _pendingViewTime.Remove(identifier);
            }
        }

        /// <summary>Drops every trace of a target. The old version only cleared the active map, so
        /// destroyed objects stayed referenced in the other two for the rest of the match.</summary>
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

            director.Enqueue(
                pool,
                Random.Range(minMessages, Mathf.Max(minMessages, maxMessages) + 1),
                isMonster ? monsterSightingIntensity : sightingIntensity,
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
                // No subject on purpose. The only name reachable from here is the GameObject's,
                // which is "Player(Clone)" - worse in a chat line than the generic fallback. Wiring
                // the real display name means reaching into Network, which this assembly cannot see.
                Raise(isLocal ? ChatTopics.PlayerDied : ChatTopics.TeammateDied, 1f);
                return;
            }

            if (current >= previous) return;

            float max = Mathf.Max(1f, health.MaxHealth);

            if ((previous - current) / max < hurtThreshold) return;

            // A teammate scraping their knee is not chat material; the streamer getting hit is.
            if (!isLocal) return;

            Raise(ChatTopics.PlayerHurt, 1f - Mathf.Clamp01(current / max));
        }

        private void ChatStimulusBus_OnStimulus(ChatStimulus stimulus) => Handle(stimulus);

        private void Raise(string topicId, float intensity) => Handle(new ChatStimulus(topicId, intensity));

        private void Handle(in ChatStimulus stimulus)
        {
            if (topicDatabase == null) return;

            if (!topicDatabase.TryGet(stimulus.TopicId, out ChatTopicEntry entry))
            {
                // Once per id: a typo in a ChatTrigger should be findable without burying the console.
                if (_warnedUnknownTopics.Add(stimulus.TopicId))
                {
                    Debug.LogWarning($"{nameof(ChatManager)}: nothing raised '{stimulus.TopicId}' " +
                                     "in the topic database, so it said nothing.", this);
                }

                return;
            }

            if (entry.data == null || entry.data.Count == 0) return;

            if (entry.cooldown > 0f && Time.time - LastTimeFor(stimulus.TopicId) < entry.cooldown)
            {
                return;
            }

            _lastTopicTime[stimulus.TopicId] = Time.time;

            // The entry sets the ceiling for the topic; the raiser scales it by how big this
            // particular one was, where 0.5 is a nominal example and lands exactly on the ceiling.
            float intensity = Mathf.Clamp01(entry.intensity * stimulus.Intensity * 2f);

            director.Enqueue(
                entry.data,
                Random.Range(entry.minMessages, Mathf.Max(entry.minMessages, entry.maxMessages) + 1),
                intensity,
                entry.mood,
                stimulus.Subject,
                entry.allowSpamWave,
                entry.priority,
                entry.ignoreViewerFloor);
        }

        private float LastTimeFor(string topicId)
        {
            return _lastTopicTime.TryGetValue(topicId, out float time) ? time : float.NegativeInfinity;
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
