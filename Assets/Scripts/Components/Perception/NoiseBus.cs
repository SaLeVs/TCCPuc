using System;
using UnityEngine;

namespace Components.Perception
{
    public static class NoiseBus
    {
        public static event Action<Noise> OnNoise;

        /// <summary>
        /// Logs every reported noise. Owned by <see cref="NoiseDebugger"/> — drop that component
        /// in the scene to turn it on rather than editing this field.
        /// </summary>
        public static bool IsVerboseLogging { get; set; }

        /// <summary>
        /// Clears every subscriber and setting.
        ///
        /// <para>Called automatically before each play session. With "Enter Play Mode Options"
        /// set to skip the domain reload — the default in Unity 6 for fast iteration — statics
        /// survive between runs, so without this the bus would still hold the previous session's
        /// destroyed HearingSensor and log against it.</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            OnNoise = null;
            IsVerboseLogging = false;
        }

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
    }
}
