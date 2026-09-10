using UnityEngine;

namespace Components.Perception
{
    public readonly struct Noise
    {
        public readonly Vector3 Position;
        public readonly float Loudness;
        public readonly NoiseType Type;

        public readonly Transform Source;

        public Noise(Vector3 position, float loudness, NoiseType type, Transform source = null)
        {
            Position = position;
            Loudness = loudness;
            Type = type;
            Source = source;
        }
    }
    
    public readonly struct HeardNoise
    {
        /// <summary>
        /// Where the listener <i>thinks</i> the noise came from — deliberately not exact.
        /// Hearing gives you a guess; only vision gives you a fix.
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>Where it actually came from. Server-side only, for debug gizmos.</summary>
        public readonly Vector3 TruePosition;

        /// <summary>0..1 — how clearly it came through. Drives how hard the monster reacts.</summary>
        public readonly float Confidence;

        /// <summary>The type of the noise.</summary>
        public readonly NoiseType Type;
        
        /// <summary>Who made it. Null for world noises. Used so a listener can ignore itself.</summary>
        public readonly Transform Source;

        public HeardNoise(Vector3 position, Vector3 truePosition, float confidence, NoiseType type, Transform source)
        {
            Position = position;
            TruePosition = truePosition;
            Confidence = confidence;
            Type = type;
            Source = source;
        }
    }
}
