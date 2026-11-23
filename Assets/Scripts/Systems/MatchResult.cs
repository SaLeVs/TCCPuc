using System;
using System.Collections.Generic;

namespace Systems
{
    /// <summary>
    /// How the last match ended, kept across the load of the Win/Lose scene. The server decides it
    /// (see Player.MatchEndDirector) and every peer receives a copy before the end scene loads, so
    /// the end scene only reads this, never the network.
    /// </summary>
    public static class MatchResult
    {
        public static bool HasValue { get; private set; }
        public static bool IsVictory { get; private set; }
        public static float DurationSeconds { get; private set; }

        /// <summary>Who got out. Empty on a defeat.</summary>
        public static IReadOnlyList<string> Survivors => _survivors;

        /// <summary>Everyone who was in the match when it ended, survivors included.</summary>
        public static IReadOnlyList<string> Players => _players;

        /// <summary>Everyone who did not get out.</summary>
        public static IReadOnlyList<string> Fallen
        {
            get
            {
                List<string> fallen = new List<string>(_players);
                foreach (string survivor in _survivors) fallen.Remove(survivor);
                return fallen;
            }
        }

        private static readonly List<string> _survivors = new();
        private static readonly List<string> _players = new();

        public static void Set(bool isVictory, float durationSeconds, IEnumerable<string> survivors, IEnumerable<string> players)
        {
            IsVictory = isVictory;
            DurationSeconds = Math.Max(0f, durationSeconds);

            _survivors.Clear();
            _survivors.AddRange(survivors);

            _players.Clear();
            _players.AddRange(players);

            HasValue = true;
        }

        public static void Clear()
        {
            HasValue = false;
            IsVictory = false;
            DurationSeconds = 0f;
            _survivors.Clear();
            _players.Clear();
        }

        /// <summary>"mm:ss", or "h:mm:ss" past an hour.</summary>
        public static string FormatDuration(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0f, seconds));
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{time.Minutes:00}:{time.Seconds:00}";
        }
    }
}
