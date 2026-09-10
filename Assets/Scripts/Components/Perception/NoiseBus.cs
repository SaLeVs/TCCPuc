using System;
using UnityEngine;

namespace Components.Perception
{
    public static class NoiseBus
    {
        public static event Action<Noise> OnNoise;

        /// <summary>Set true to log every reported noise while tuning.</summary>
        public static bool IsVerboseLogging;

        public static void Report(Noise noise)
        {
            if (noise.Loudness <= 0f) return;

            if (IsVerboseLogging)
            {
                Debug.Log($"NoiseBus: {noise.Type} loudness={noise.Loudness:0.0}m at {noise.Position}");
            }

            OnNoise?.Invoke(noise);
        }

        public static void Report(Vector3 position, float loudness, NoiseType type, Transform source = null)
        {
            Report(new Noise(position, loudness, type, source));
        }
        
        
        public static void Reset()
        {
            OnNoise = null;
        }
    }
}
