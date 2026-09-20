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
    [Serializable]
    public class ChatDirector
    {
        /// <summary>Fired for each released line, already formatted. Viewer name, then text.</summary>
        public event Action<string, string> OnLine;

        [Header("Volume")]
        [SerializeField]
        [Tooltip("Lines per minute against the number of people watching. This is the main balance " +
                 "dial for the whole chat: X is the viewer count, Y is how many lines a minute a " +
                 "crowd that size produces when nothing in particular is happening.")]
        private AnimationCurve linesPerMinuteByViewers = DefaultViewerCurve();

        [SerializeField, Min(1f)]
        [Tooltip("How much faster chat talks at full intensity than at rest. Multiplies whatever " +
                 "the viewer curve gave, so a big moment scales with the size of the room instead " +
                 "of flooding a tiny one.")]
        private float peakIntensityMultiplier = 6f;

        [SerializeField, Min(0f)]
        [Tooltip("Viewers needed before chat says anything at all. Below this only topics marked " +
                 "Ignore Viewer Floor get through - which is how a hint still reaches a player " +
                 "whose stream nobody is watching yet.")]
        private float minViewersToTalk = 1f;

        [Header("Pacing")]
        [SerializeField, Min(0.05f)] private float minGap = 0.35f;
        [SerializeField, Min(0.1f)] private float maxGap = 20f;

        [SerializeField, Min(0f)]
        [Tooltip("How much intensity bleeds off per second. 0.12 means a full-blown reaction takes " +
                 "about eight seconds to settle back down.")]
        private float intensityDecayPerSecond = 0.12f;

        [Header("Ambient")]
        [SerializeField]
        [Tooltip("Keep a trickle of small talk going when nothing is happening. Off means chat goes " +
                 "silent whenever the player is not looking at anything, which is the single most " +
                 "obvious tell that it is not real.")]
        private bool ambientEnabled = true;

        [Header("Spam wave")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Intensity a topic has to reach before several viewers pile onto the same short " +
                 "line. Deliberate repetition reads as a real chat losing it; accidental repetition " +
                 "reads as a bug.")]
        private float spamWaveThreshold = 0.75f;

        [SerializeField, Min(2)] private int spamWaveMin = 3;
        [SerializeField, Min(2)] private int spamWaveMax = 6;
        [SerializeField, Min(0.02f)] private float spamWaveGap = 0.12f;

        [Header("Limits")]
        [SerializeField, Min(1)]
        [Tooltip("Lines allowed to wait in the queue. Past this the lowest-priority ones are " +
                 "dropped, so a flood never pushes a hint out of the way.")]
        private int maxQueued = 14;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds a line may wait before it is dropped unsaid. Without this a backed-up " +
                 "queue has chat still screaming about a death long after the player respawned.")]
        private float maxLineAge = 12f;

        [SerializeField, Min(1)]
        [Tooltip("Tries at finding a viewer whose personality fits the pool before giving up on " +
                 "the personality filter for that line.")]
        private int speakerAttempts = 4;

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
            public readonly string Viewer;
            public readonly string Text;
            public readonly int Priority;
            public readonly float EnqueuedAt;
            public readonly bool IgnoreViewerFloor;

            /// <summary>Negative means draw a gap from the distribution.</summary>
            public readonly float GapOverride;

            public PendingLine(string viewer, string text, int priority, float gapOverride,
                bool ignoreViewerFloor)
            {
                Viewer = viewer;
                Text = text;
                Priority = priority;
                GapOverride = gapOverride;
                IgnoreViewerFloor = ignoreViewerFloor;
                EnqueuedAt = Time.unscaledTime;
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

            if (minGap <= 0f) minGap = 0.35f;
            if (maxGap <= minGap) maxGap = Mathf.Max(minGap + 0.1f, 20f);

            if (intensityDecayPerSecond <= 0f) intensityDecayPerSecond = 0.12f;

            if (spamWaveMin < 2) spamWaveMin = 3;
            if (spamWaveMax < spamWaveMin) spamWaveMax = Mathf.Max(spamWaveMin, 6);
            if (spamWaveGap <= 0f) spamWaveGap = 0.12f;

            if (maxQueued < 1) maxQueued = 14;
            if (maxLineAge < 1f) maxLineAge = 12f;
            if (speakerAttempts < 1) speakerAttempts = 4;
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
            string subject, bool allowSpamWave, int priority = 0, bool ignoreViewerFloor = false)
        {
            if (pool == null || pool.Count == 0 || count <= 0) return;

            Excite(intensity, mood);

            if (allowSpamWave && intensity >= spamWaveThreshold &&
                TryQueueSpamWave(pool, subject, priority, ignoreViewerFloor))
            {
                return;
            }

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
                _gapTimer = minGap;
                return;
            }

            PendingLine next = _queue[index];
            _queue.RemoveAt(index);

            OnLine?.Invoke(next.Viewer, next.Text);

            _silence = 0f;
            _gapTimer = next.GapOverride >= 0f ? next.GapOverride : DrawGap(viewerCount);
        }

        /// <summary>
        /// First line that may go out. With nobody watching, only what is marked to ignore the
        /// viewer floor - a hint the player still needs - gets through.
        /// </summary>
        private int NextReleasable(bool roomIsWatching)
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (roomIsWatching || _queue[i].IgnoreViewerFloor) return i;
            }

            return -1;
        }

        private void DropStaleLines()
        {
            float now = Time.unscaledTime;

            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (now - _queue[i].EnqueuedAt <= maxLineAge) continue;

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

            return Mathf.Clamp(-Mathf.Log(uniform) / rate, minGap, maxGap);
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

        private bool TryQueueSpamWave(TargetChatData pool, string subject, int priority,
            bool ignoreViewerFloor)
        {
            if (!TryDraw(pool, out string firstViewer, out ChatMessage line)) return false;
            if (!line.spammable) return false;

            int count = Random.Range(spamWaveMin, spamWaveMax + 1);
            string text = Format(line.message, subject);

            Push(firstViewer, text, priority, -1f, ignoreViewerFloor);

            for (int i = 1; i < count; i++)
            {
                if (_viewers == null || !_viewers.TryPick(out ViewerProfile profile)) break;

                Push(profile.name, text, priority, spamWaveGap, ignoreViewerFloor);
            }

            return true;
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

            for (int attempt = 0; attempt < speakerAttempts; attempt++)
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

            if (_queue.Count >= maxQueued)
            {
                // Full. The new line only gets in by outranking something already waiting,
                // otherwise chat simply never gets around to saying it.
                if (priority <= _queue[^1].Priority) return;

                _queue.RemoveAt(_queue.Count - 1);
            }

            int index = _queue.Count;

            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Priority >= priority) continue;

                index = i;
                break;
            }

            _queue.Insert(index, new PendingLine(viewer, text, priority, gapOverride, ignoreViewerFloor));
        }

        private static string Format(string message, string subject)
        {
            if (string.IsNullOrEmpty(message)) return message;

            if (!message.Contains(ChatMessage.SubjectToken)) return message;

            return message.Replace(ChatMessage.SubjectToken,
                string.IsNullOrWhiteSpace(subject) ? "alguem" : subject);
        }
    }
}
