using System;
using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace Chat
{
    [Serializable]
    public class ChatMessage
    {
        [TextArea(2, 4)]
        public string message;

        [Tooltip("Which personalities are allowed to post this line.")]
        public ViewerArchetype allowedArchetypes = ViewerArchetype.Everyone;

        /// <summary>Placeholder a line can use to name the donor, the player who died, and so on.</summary>
        public const string SUBJECT_TOKEN = "{subject}";
    }

    /// <summary>
    /// One pool of lines - everything chat might say about a single target, stimulus or mood.
    /// </summary>
    [Serializable]
    public class TargetChatData
    {
        public List<ChatMessage> messages = new();

        /// <summary>
        /// Seconds a line takes to get its full weight back after being posted, and the multiplier
        /// it drops to the instant it is used.
        ///
        /// <para>Constants rather than fields: every pool in the game wants the same answer to
        /// "don't say that again right away", and exposing them meant two numbers on each of the
        /// twenty pools - forty inspector rows nobody was ever going to tune individually.</para>
        /// </summary>
        private const float RECENCY_WINDOW = 45f;
        private const float RECENCY_FLOOR = 0.1f;

        /// <summary>
        /// When each line was last posted. Runtime only and rebuilt on demand: this class is
        /// serialized inside ScriptableObjects, and an asset must not carry a play session's
        /// history into the next one.
        /// </summary>
        [NonSerialized] private float[] _lastUsed;

        public int Count => messages?.Count ?? 0;

        public void ResetRuntimeState() => _lastUsed = null;

        /// <summary>Every personality, one bit each, for walking the archetype filter.</summary>
        private static readonly ViewerArchetype[] AllArchetypes =
        {
            ViewerArchetype.Hype, ViewerArchetype.Scared, ViewerArchetype.Troll,
            ViewerArchetype.Backseat, ViewerArchetype.Lurker
        };

        /// <summary>
        /// Personalities that have fewer than <paramref name="minimum"/> lines they are allowed to
        /// say, as a flags value. None means the pool is healthy.
        ///
        /// <para>This catches the quiet failure mode of the archetype filter: a pool can look full
        /// while a given personality only has one line in it, and then every viewer of that
        /// personality says the same thing every single time. The recency suppression cannot help,
        /// because there is nothing else to pick.</para>
        /// </summary>
        public ViewerArchetype NarrowArchetypes(int minimum)
        {
            ViewerArchetype narrow = ViewerArchetype.None;

            if (Count == 0) return narrow;

            foreach (ViewerArchetype archetype in AllArchetypes)
            {
                int eligible = 0;

                foreach (ChatMessage entry in messages)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.message)) continue;
                    if ((entry.allowedArchetypes & archetype) == 0) continue;

                    eligible++;
                }

                if (eligible < minimum) narrow |= archetype;
            }

            return narrow;
        }

        /// <summary>
        /// Draws a line for a viewer of <paramref name="archetype"/>, weighted and biased away from
        /// whatever was said recently.
        ///
        /// <para>Returns false rather than throwing when the pool is empty or has nothing this
        /// personality is allowed to say - an unfilled pool is a content gap to warn about, not a
        /// crash.</para>
        /// </summary>
        public bool TryDraw(ViewerArchetype archetype, out ChatMessage picked)
        {
            picked = null;

            int count = Count;
            if (count == 0) return false;

            EnsureHistory(count);

            float now = Time.unscaledTime;
            float total = 0f;

            for (int i = 0; i < count; i++)
            {
                total += EffectiveWeight(i, archetype, now);
            }

            // Every candidate is either disabled, off-limits to this personality, or the pool is a
            // single line that was just posted. Caller decides whether to skip or relax.
            if (total <= 0f) return false;

            float roll = UnityEngine.Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < count; i++)
            {
                float weight = EffectiveWeight(i, archetype, now);
                if (weight <= 0f) continue;

                cumulative += weight;

                // Strictly-less so a line at index 0 that is currently ineligible cannot win on a
                // roll of exactly 0.
                if (roll < cumulative)
                {
                    picked = messages[i];
                    _lastUsed[i] = now;
                    return true;
                }
            }

            // Float drift only. Fall through to the last line that was actually eligible.
            for (int i = count - 1; i >= 0; i--)
            {
                if (EffectiveWeight(i, archetype, now) <= 0f) continue;

                picked = messages[i];
                _lastUsed[i] = now;
                return true;
            }

            return false;
        }

        /// <summary>
        /// How likely this line is right now: zero when it is not eligible at all, otherwise purely
        /// how long ago it was last said.
        ///
        /// <para>Every eligible line starts equal. An earlier version let each line carry an
        /// authored weight, which pulled against the recency suppression sitting right next to it -
        /// one spreading lines out, the other concentrating them - and made a handful of lines
        /// recur while the rest almost never came up.</para>
        /// </summary>
        private float EffectiveWeight(int index, ViewerArchetype archetype, float now)
        {
            ChatMessage entry = messages[index];

            if (entry == null || string.IsNullOrWhiteSpace(entry.message)) return 0f;
            if ((entry.allowedArchetypes & archetype) == 0) return 0f;

            return RecencyMultiplier(index, now);
        }

        private float RecencyMultiplier(int index, float now)
        {
            float last = _lastUsed[index];

            if (float.IsNegativeInfinity(last)) return 1f;

            // Play mode restarted without a domain reload, so the stamp is from the previous run
            // and is now in the future. Treat the line as untouched.
            if (last > now)
            {
                _lastUsed[index] = float.NegativeInfinity;
                return 1f;
            }

            return Mathf.Lerp(RECENCY_FLOOR, 1f, Mathf.Clamp01((now - last) / RECENCY_WINDOW));
        }

        private void EnsureHistory(int count)
        {
            if (_lastUsed != null && _lastUsed.Length == count) return;

            _lastUsed = new float[count];

            for (int i = 0; i < count; i++)
            {
                _lastUsed[i] = float.NegativeInfinity;
            }
        }
    }
}
