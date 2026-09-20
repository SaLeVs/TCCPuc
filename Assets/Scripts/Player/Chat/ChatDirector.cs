using System;
using System.Collections.Generic;
using Chat;
using Enums;
using ScriptableObjects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Player.Chat
{
    /// <summary>
    /// Decides who says what, when, and how much of it.
    ///
    /// <para>Everything that wants chat to react hands this a pool and a volume; it draws the
    /// speakers and the lines up front, then releases them one at a time on its own clock. The
    /// single clock is the point: the first version started one coroutine per target, so walking
    /// into a room with five props opened five coroutines that all posted at once.</para>
    ///
    /// <para>Two rules shape the result. Volume comes from how many people are actually watching,
    /// read off a curve - ten viewers cannot produce the same wall of text as a thousand. And
    /// delivery is a priority queue rather than a plain one, so a hint telling the player where the
    /// generator is never sits behind six jokes about a chair.</para>
    /// </summary>
    /// <remarks>A component rather than a field inside <see cref="ChatManager"/>: it owns nine
    /// tuning values, and nesting them buried the whole thing behind a foldout inside another
    /// component. Driven by ChatManager instead of by its own Update, so it starts, stops and
    /// clears exactly when the chat does.</remarks>
    [DisallowMultipleComponent]
    public class ChatDirector : MonoBehaviour
    {
        /// <summary>Fired for each released line, already formatted. Viewer name, then text.</summary>
        public event Action<string, string> OnLine;

        [Header("Volume")]
        [SerializeField]
        [Tooltip("Lines per minute against the number of people watching")]
        private AnimationCurve linesPerMinuteByViewers = DefaultViewerCurve();

        [SerializeField, Min(1f)]
        [Tooltip("How much faster chat talks at full intensity than at rest")]
        private float peakIntensityMultiplier = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("Viewers needed before chat says anything at all")]
        private float minViewersToTalk = 1f;

        [Header("Pacing")]
        [SerializeField, Min(0.05f)] private float minGapBetweenMessage = 0.35f;
        [SerializeField, Min(0.1f)] private float maxGapBetweenMessage = 20f;

        [SerializeField, Min(0f)]
        [Tooltip("How much intensity bleeds off per second")]
        private float intensityDecayPerSecond = 0.12f;

        [Header("Ambient")]
        [SerializeField]
        [Tooltip("Keep a trickle of small talk going when nothing is happening")]
        private bool ambientEnabled = true;

        [Header("Limits")]
        [SerializeField, Min(1)]
        [Tooltip("Past this, room is made by dropping the oldest line of the lowest-priority band")]
        private int maxQueuedMessages = 14;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds a line may wait before it is dropped unsaid")]
        private float maxLineAge = 12f;

        /// <summary>
        /// Tries at finding a viewer whose personality fits the pool before dropping the filter.
        /// A constant, not a field: it is a detail of how the draw retries, not a decision anyone
        /// tuning the chat would ever want to make.
        /// </summary>
        private const int SpeakerAttempts = 5;

        /// <summary>
        /// Sorted by priority descending, then by insertion order. A List rather than a Queue
        /// because a hint has to be able to overtake whatever small talk is already waiting, and at
        /// fourteen entries the cost of an ordered insert is not worth a heap.
        /// </summary>
        private readonly List<PendingLine> _queue = new();

        private ViewerPopulationSO _viewers;
        private ChatAmbientDatabaseSO _ambient;

        private float _gapTimer;
        private float _intensity;
        private float _silence;
        private float _moodHold;
        private ChatMood _mood = ChatMood.Idle;

        public ChatMood Mood => _mood;
        public float Intensity => _intensity;
        public int Queued => _queue.Count;

        private readonly struct PendingLine
        {
            public readonly string viewer;
            public readonly string text;
            public readonly int priority;
            public readonly float enqueuedAt;
            public readonly bool ignoreViewerFloor;

            /// <summary>Negative means draw a gap from the distribution.</summary>
            public readonly float gapOverride;

            public PendingLine(string viewer, string text, int priority, float gapOverride,
                bool ignoreViewerFloor)
            {
                this.viewer = viewer;
                this.text = text;
                this.priority = priority;
                this.gapOverride = gapOverride;
                this.ignoreViewerFloor = ignoreViewerFloor;
                enqueuedAt = Time.unscaledTime;
            }
        }


        public void Initialize(ViewerPopulationSO viewers, ChatAmbientDatabaseSO ambient)
        {
            _viewers = viewers;
            _ambient = ambient;

            Sanitize();
            Clear();
        }

        /// <summary>
        /// Puts any unusable tuning value back to a working default.
        ///
        /// <para>Not paranoia: this class was added to a MonoBehaviour that is already serialized on
        /// a prefab, so on the first load there is no data for it and the field initializers are not
        /// guaranteed to survive deserialization. A zeroed maxGap makes Clamp collapse every gap to
        /// zero and the chat posts on every frame; a zeroed maxQueued makes the overflow check run
        /// on an empty list. A curve with no keys evaluates to zero everywhere, which would mute
        /// the chat completely and look like a bug in the pacing rather than missing data.</para>
        /// </summary>
        private void Sanitize()
        {
            if (linesPerMinuteByViewers == null || linesPerMinuteByViewers.length < 2)
            {
                linesPerMinuteByViewers = DefaultViewerCurve();
            }

            if (peakIntensityMultiplier < 1f) peakIntensityMultiplier = 6f;

            if (minGapBetweenMessage <= 0f) minGapBetweenMessage = 0.35f;
            if (maxGapBetweenMessage <= minGapBetweenMessage) maxGapBetweenMessage = Mathf.Max(minGapBetweenMessage + 0.1f, 20f);

            if (intensityDecayPerSecond <= 0f) intensityDecayPerSecond = 0.12f;

            if (maxQueuedMessages < 1) maxQueuedMessages = 14;
            if (maxLineAge < 1f) maxLineAge = 12f;
        }

        /// <summary>
        /// Roughly how a real chat scales: the first handful of viewers barely talk, and the rate
        /// keeps climbing but never in step with the headcount.
        /// </summary>
        private static AnimationCurve DefaultViewerCurve()
        {
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(10f, 1.5f),
                new Keyframe(50f, 4f),
                new Keyframe(150f, 8f),
                new Keyframe(400f, 16f),
                new Keyframe(800f, 25f),
                new Keyframe(1200f, 34f));

            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }

        public void Clear()
        {
            _queue.Clear();

            _gapTimer = 0f;
            _intensity = 0f;
            _silence = 0f;
            _moodHold = 0f;
            _mood = ChatMood.Idle;

            _viewers?.ResetRuntimeState();
        }

        /// <summary>
        /// Queues a reaction. Drawing happens here, not at release time, so the rule about not
        /// repeating the last few speakers applies across a burst instead of only between bursts.
        /// </summary>
        public void Enqueue(TargetChatData pool, int count, float intensity, ChatMood mood,
            string subject, int priority = 0, bool ignoreViewerFloor = false)
        {
            if (pool == null || pool.Count == 0 || count <= 0) return;

            Excite(intensity, mood);

            for (int i = 0; i < count; i++)
            {
                if (!TryDraw(pool, out string viewer, out ChatMessage line)) return;

                Push(viewer, Format(line.message, subject), priority, -1f, ignoreViewerFloor);
            }
        }

        /// <summary>Raises intensity and pushes the mood, never lowering either.</summary>
        public void Excite(float intensity, ChatMood mood)
        {
            _intensity = Mathf.Clamp01(Mathf.Max(_intensity, intensity));

            if (mood == ChatMood.Idle) return;

            // Hold scales with intensity: a jump scare owns the room longer than a small donation.
            float hold = Mathf.Lerp(2f, 12f, Mathf.Clamp01(intensity));

            if (_moodHold > hold && _mood == mood) return;

            _mood = mood;
            _moodHold = Mathf.Max(_moodHold, hold);
        }

        public void Tick(float deltaTime, float viewerCount, bool audienceDecaying)
        {
            _intensity = Mathf.Max(0f, _intensity - intensityDecayPerSecond * deltaTime);

            TickMood(deltaTime, audienceDecaying);
            DropStaleLines();

            _gapTimer -= deltaTime;
            _silence += deltaTime;

            if (_gapTimer > 0f) return;

            bool roomIsWatching = viewerCount >= minViewersToTalk;

            if (_queue.Count == 0 && roomIsWatching)
            {
                TryQueueAmbient(viewerCount);
            }

            int index = NextReleasable(roomIsWatching);

            if (index < 0)
            {
                // Nothing to say, or nobody to say it to. Check again shortly rather than every frame.
                _gapTimer = minGapBetweenMessage;
                return;
            }

            PendingLine next = _queue[index];
            _queue.RemoveAt(index);

            OnLine?.Invoke(next.viewer, next.text);

            _silence = 0f;
            _gapTimer = next.gapOverride >= 0f ? next.gapOverride : DrawGap(viewerCount);
        }

        /// <summary>
        /// First line that may go out. With nobody watching, only what is marked to ignore the
        /// viewer floor - a hint the player still needs - gets through.
        /// </summary>
        private int NextReleasable(bool roomIsWatching)
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (roomIsWatching || _queue[i].ignoreViewerFloor) return i;
            }

            return -1;
        }

        private void DropStaleLines()
        {
            float now = Time.unscaledTime;

            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (now - _queue[i].enqueuedAt <= maxLineAge) continue;

                _queue.RemoveAt(i);
            }
        }

        private void TickMood(float deltaTime, bool audienceDecaying)
        {
            if (_moodHold > 0f)
            {
                _moodHold -= deltaTime;
                if (_moodHold > 0f) return;
            }

            // Nothing is holding the room in a mood, so it falls back to whether the audience is
            // still interested.
            _mood = audienceDecaying ? ChatMood.Bored : ChatMood.Idle;
        }

        /// <summary>
        /// Exponential inter-arrival time: the gap between two independent messages in a real chat
        /// is memoryless, which is what makes it clump instead of tick.
        /// </summary>
        private float DrawGap(float viewerCount)
        {
            float perMinute = linesPerMinuteByViewers.Evaluate(viewerCount);

            perMinute *= Mathf.Lerp(1f, peakIntensityMultiplier, _intensity);

            float rate = Mathf.Max(0.001f, perMinute / 60f);

            // Guard the log: Random.value can come back as exactly 1.
            float uniform = Mathf.Max(1e-6f, 1f - Random.value);

            return Mathf.Clamp(-Mathf.Log(uniform) / rate, minGapBetweenMessage, maxGapBetweenMessage);
        }

        private void TryQueueAmbient(float viewerCount)
        {
            if (!ambientEnabled || _ambient == null) return;
            if (!_ambient.TryGet(_mood, out MoodChatEntry entry)) return;
            if (entry.data == null || entry.data.Count == 0) return;

            // Small talk thins out with the room instead of keeping a fixed cadence: a handful of
            // viewers should feel like a handful of viewers.
            float perMinute = Mathf.Max(0.01f, linesPerMinuteByViewers.Evaluate(viewerCount));
            float scale = Mathf.Clamp(6f / perMinute, 0.4f, 6f);

            if (_silence < entry.secondsBetweenLines * scale) return;

            if (!TryDraw(entry.data, out string viewer, out ChatMessage line)) return;

            Push(viewer, Format(line.message, null), 0, -1f, false);
        }

        /// <summary>
        /// Picks a speaker, then a line that speaker's personality is allowed to post. Retries with
        /// other speakers before dropping the personality filter, so a pool written for one
        /// archetype still gets said instead of falling silent.
        /// </summary>
        private bool TryDraw(TargetChatData pool, out string viewer, out ChatMessage line)
        {
            viewer = null;
            line = null;

            if (_viewers == null || pool == null) return false;

            for (int attempt = 0; attempt < SpeakerAttempts; attempt++)
            {
                if (!_viewers.TryPick(out ViewerProfile profile)) return false;

                if (pool.TryDraw(profile.archetype, out line))
                {
                    viewer = profile.name;
                    return true;
                }

                viewer = profile.name;
            }

            return viewer != null && pool.TryDraw(ViewerArchetype.Everyone, out line);
        }

        /// <summary>
        /// Inserts ordered by priority, keeping insertion order within a priority so a burst still
        /// reads in the order it was written.
        /// </summary>
        private void Push(string viewer, string text, int priority, float gapOverride,
            bool ignoreViewerFloor)
        {
            if (string.IsNullOrEmpty(viewer) || string.IsNullOrEmpty(text)) return;

            if (_queue.Count >= maxQueuedMessages && !TryMakeRoom(priority)) return;

            int index = _queue.Count;

            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].priority >= priority) continue;

                index = i;
                break;
            }

            _queue.Insert(index, new PendingLine(viewer, text, priority, gapOverride, ignoreViewerFloor));
        }

        /// <summary>
        /// Evicts one line so a new one of <paramref name="priority"/> can take its place, or
        /// returns false if the new line is the weakest thing in play and should be dropped instead.
        ///
        /// <para>The victim is the oldest member of the lowest-priority band. Priority decides
        /// which band gets sacrificed, so a flood of small talk still cannot push a hint out; age
        /// decides who inside that band goes, so a full queue keeps reacting to now instead of
        /// holding stale lines and turning fresh ones away.</para>
        /// </summary>
        private bool TryMakeRoom(int priority)
        {
            int victim = _queue.Count - 1;
            int lowest = _queue[victim].priority;

            // Priority-descending with arrival order kept inside each band, so the lowest band sits
            // at the end and its oldest member is that band's first entry.
            while (victim > 0 && _queue[victim - 1].priority == lowest)
            {
                victim--;
            }

            if (priority < lowest) return false;

            _queue.RemoveAt(victim);

            return true;
        }

        private static string Format(string message, string subject)
        {
            if (string.IsNullOrEmpty(message)) return message;
            if (!message.Contains(ChatMessage.SUBJECT_TOKEN)) return message;

            return message.Replace(ChatMessage.SUBJECT_TOKEN, string.IsNullOrWhiteSpace(subject) ? "Someone" : subject);
        }
    }
}
