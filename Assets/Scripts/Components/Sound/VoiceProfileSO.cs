using UnityEngine;

namespace Components.Sound
{
    /// <summary>
    /// How loud a voice is and how far it carries, in one asset for everyone who reads the mic.
    ///
    /// <para>The speaker's own PlayerMicReporter (what the monster hears) and every listener's
    /// RemoteVoiceFilter (what the other players hear) run on different machines and can never
    /// share an instance, so they used to keep their own copies of these thresholds — and the copies
    /// had already drifted apart once. Both read this instead.</para>
    ///
    /// <para>The energies are Vivox's AudioEnergy, 0..1, measured with Vivox's automatic gain
    /// control OFF. With AGC on, a shout and a normal sentence come out at nearly the same level and
    /// these numbers stop meaning anything. Calibrate them with the voice meter in Options → Audio
    /// device.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceProfile", menuName = "ScriptableObjects/Audio/Voice Profile")]
    public class VoiceProfileSO : ScriptableObject
    {
        [Header("Mic energy (Vivox AudioEnergy, AGC off)")]
        [Tooltip("At or below this the voice counts as a whisper: it reaches only Whisper Range, and " +
                 "the monster does not hear it.")]
        [SerializeField, Range(0f, 1f)] private float speechEnergy = 0.25f;

        [Tooltip("At or above this it counts as a shout: it reaches the voice channel's audible " +
                 "distance, and the monster hears it from the shout distance.")]
        [SerializeField, Range(0f, 1f)] private float shoutEnergy = 0.65f;

        [Tooltip("At or above this the player counts as talking for the donation missions.")]
        [SerializeField, Range(0f, 1f)] private float talkingEnergy = 0.65f;

        [Header("Distance")]
        [Tooltip("How far a whisper carries, in meters. A shout carries to the channel's audible distance.")]
        [SerializeField, Min(0f)] private float whisperRange = 8f;

        [Tooltip("How far normal talking carries, in meters. While someone talks the range never drops " +
                 "below this, so it no longer collapses towards the whisper range between syllables.")]
        [SerializeField, Min(0f)] private float speechRange = 24f;

        [Tooltip("Inside this distance, in meters, a voice plays at the level it was spoken at. Past " +
                 "it the level drops by Decibels Per Doubling every time the distance doubles.")]
        [SerializeField, Min(0.1f)] private float nearField = 1f;

        [Tooltip("How much quieter a voice gets every time the distance doubles past the near field. " +
                 "6 is the free-field inverse-distance law; a building's reflections keep a real voice " +
                 "louder than that, and so does a lower value here.")]
        [SerializeField, Range(1f, 6f)] private float decibelsPerDoubling = 4.5f;

        [Tooltip("Fraction of the range where the voice starts closing to silence. Before it only the " +
                 "distance law acts; after it a cosine closes the rest, so the end of the range is a " +
                 "fade, not a cut.")]
        [SerializeField, Range(0f, 0.95f)] private float edgeFadeStart = 0.6f;

        [Header("Heard voice (Vivox tap → energy)")]
        [Tooltip("Level, in dBFS, of a voice as it arrives through its Vivox tap that reads as energy 0. " +
                 "Vivox reports AudioEnergy 0 for every tapped participant, so a remote voice's loudness " +
                 "is measured from the tap instead; this and the next value put it on the same scale. " +
                 "Measured 2026-11-01: energy 0,45 ≈ -44 dB, 0,65 ≈ -22 dB.")]
        [SerializeField] private float heardSilenceDb = -97f;

        [Tooltip("Level, in dBFS, of a voice arriving through its tap that reads as energy 1.")]
        [SerializeField] private float heardFullDb = 21f;

        public float SpeechEnergy => speechEnergy;
        public float ShoutEnergy => shoutEnergy;
        public float TalkingEnergy => talkingEnergy;
        public float WhisperRange => whisperRange;
        public float SpeechRange => speechRange;
        public float NearField => nearField;
        public float EdgeFadeStart => edgeFadeStart;

        /// <summary>0 for a whisper, 1 for a shout, linear in between.</summary>
        public float Loudness(float energy) => Mathf.InverseLerp(speechEnergy, shoutEnergy, energy);

        /// <summary>A voice's level as it arrives through its tap, in dBFS, on Vivox's AudioEnergy scale.</summary>
        public float EnergyFromHeardLevel(float dbfs) => Mathf.InverseLerp(heardSilenceDb, heardFullDb, dbfs);

        /// <summary>The distance law as a gain: 1 inside the near field, then Decibels Per Doubling.</summary>
        public float DistanceGain(float distance)
        {
            if (distance <= nearField) return 1f;

            float doublings = Mathf.Log(distance / nearField, 2f);
            return Mathf.Pow(10f, -decibelsPerDoubling * doublings / 20f);
        }

        /// <summary>
        /// The distance law as an AudioSource custom rolloff, which Steam Audio's curve-driven
        /// attenuation follows. Custom curves run over 0..1 of <paramref name="maxDistance"/>.
        /// </summary>
        public AnimationCurve BuildRolloff(float maxDistance)
        {
            maxDistance = Mathf.Max(maxDistance, nearField + 0.1f);

            // Squared spacing puts most keys close in, where the curve bends.
            var keys = new Keyframe[32];

            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / (keys.Length - 1f);
                float distance = t * t * maxDistance;
                keys[i] = new Keyframe(distance / maxDistance, DistanceGain(distance));
            }

            var rolloff = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) rolloff.SmoothTangents(i, 0f);

            return rolloff;
        }

        private void OnValidate()
        {
            // Mathf.InverseLerp silently inverts when these cross, which would make speaking
            // quietly the thing that carries furthest.
            shoutEnergy = Mathf.Max(shoutEnergy, speechEnergy);
            speechRange = Mathf.Max(speechRange, whisperRange);
        }
    }
}
