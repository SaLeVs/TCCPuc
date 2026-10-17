using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Gym
{
    /// <summary>
    /// Times a run between pads. "How long does it take to walk this?" is the question the course
    /// says to answer before deciding what a space needs, so the GYM answers it in seconds.
    /// </summary>
    public static class GymStopwatch
    {
        public readonly struct Mark
        {
            public readonly string Label;
            public readonly float Seconds;

            public Mark(string label, float seconds)
            {
                Label = label;
                Seconds = seconds;
            }
        }

        private static readonly List<Mark> MarksList = new();
        private static float _startTime = -1f;
        private static float _stopTime = -1f;

        public static string RunName { get; private set; } = string.Empty;
        public static IReadOnlyList<Mark> Marks => MarksList;
        public static bool IsRunning => _startTime >= 0f && _stopTime < 0f;
        public static bool HasResult => _startTime >= 0f;

        public static float Elapsed => !HasResult ? 0f : (IsRunning ? Time.time : _stopTime) - _startTime;

        public static void Begin(string runName)
        {
            RunName = runName ?? string.Empty;
            MarksList.Clear();
            _startTime = Time.time;
            _stopTime = -1f;
        }

        public static void Split(string label)
        {
            if (!IsRunning) return;
            MarksList.Add(new Mark(label, Elapsed));
        }

        public static void Stop(string label)
        {
            if (!IsRunning) return;
            MarksList.Add(new Mark(label, Elapsed));
            _stopTime = Time.time;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MarksList.Clear();
            _startTime = -1f;
            _stopTime = -1f;
            RunName = string.Empty;
        }
    }
}
