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

        [Min(0f)]
        [Tooltip("Relative chance against the other lines in this pool. 0 disables the line.")]
        public float weight = 1f;

        [Tooltip("Which personalities are allowed to post this line.")]
        public ViewerArchetype allowedArchetypes = ViewerArchetype.Everyone;

        [Tooltip("Short enough to work as a spam wave")]
        public bool spammable;

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

        [Header("Repetition")]
        [Tooltip("Seconds a line takes to get its full weight back after being posted")]
        [Min(0f)] public float recencyWindow = 45f;

        [Tooltip("Weight multiplier a line drops to the instant it is posted")]
        [Range(0f, 1f)] public float recencyFloor = 0.1f;

        /// <summary>
        /// When each line was last posted. Runtime only and rebuilt on demand: this class is
        /// serialized inside ScriptableObjects, and an asset must not carry a play session's
        /// history into the next one.
        /// </summary>
        [NonSerialized] private float[] _lastUsed;

        public int Count => messages?.Count ?? 0;

        public void ResetRuntimeState() => _lastUsed = null;

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

                // Strictly-less keeps a zero-weight line at index 0 from winning on a roll of
                // exactly 0, which is how the previous version could post a disabled line.
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

        private float EffectiveWeight(int index, ViewerArchetype archetype, float now)
        {
            ChatMessage entry = messages[index];

            if (entry == null || string.IsNullOrWhiteSpace(entry.message)) return 0f;
            if (entry.weight <= 0f) return 0f;
            if ((entry.allowedArchetypes & archetype) == 0) return 0f;

            return entry.weight * RecencyMultiplier(index, now);
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

            if (recencyWindow <= 0f) return 1f;

            return Mathf.Lerp(recencyFloor, 1f, Mathf.Clamp01((now - last) / recencyWindow));
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
