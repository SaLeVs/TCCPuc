using System.Collections.Generic;
using UnityEngine;

namespace Missions.Donations
{
    /// <summary>
    /// Who is reading a donation out loud right now, as far as this machine can tell.
    ///
    /// <para>The reading is TTS sent through the recipient's own voice, so on everyone else's machine
    /// it arrives as that player talking — and a voice's range follows how loud the mic is, which
    /// the TTS is not. The voice filter asks here instead, and carries a reading as far as a shout.</para>
    ///
    /// <para>Vivox has no callback for when TTS finishes, so the window is an estimate from the text.
    /// Every client fills it in from the same replicated donation state, so they all agree.</para>
    /// </summary>
    public static class DonationReading
    {
        private static readonly Dictionary<ulong, float> ReadingUntil = new();

        public static void Mark(ulong clientId, float seconds)
        {
            float until = Time.unscaledTime + seconds;

            // A second reading queued behind the first extends the window instead of cutting it.
            if (ReadingUntil.TryGetValue(clientId, out float current) && current > Time.unscaledTime)
            {
                until = current + seconds;
            }

            ReadingUntil[clientId] = until;
        }

        public static bool IsReading(ulong clientId)
        {
            return ReadingUntil.TryGetValue(clientId, out float until) && Time.unscaledTime < until;
        }

        /// <summary>Seconds a TTS voice takes to read <paramref name="text"/>, roughly.</summary>
        public static float EstimateSeconds(string text, float wordsPerSecond)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0f;

            int words = text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Length;

            return words / Mathf.Max(0.1f, wordsPerSecond);
        }
    }
}
