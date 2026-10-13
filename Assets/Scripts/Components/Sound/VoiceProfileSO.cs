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

        [Tooltip("Inside this distance, in meters, a voice plays at the level it was spoken at. Past " +
                 "it the level follows the inverse-distance law: about 6 dB quieter every time the " +
                 "distance doubles. Real voices are measured at 1 m.")]
        [SerializeField, Min(0.1f)] private float nearField = 1f;

        public float SpeechEnergy => speechEnergy;
        public float ShoutEnergy => shoutEnergy;
        public float TalkingEnergy => talkingEnergy;
        public float WhisperRange => whisperRange;
        public float NearField => nearField;

        /// <summary>0 for a whisper, 1 for a shout, linear in between.</summary>
        public float Loudness(float energy) => Mathf.InverseLerp(speechEnergy, shoutEnergy, energy);

        private void OnValidate()
        {
            // Mathf.InverseLerp silently inverts when these cross, which would make speaking
            // quietly the thing that carries furthest.
            shoutEnergy = Mathf.Max(shoutEnergy, speechEnergy);
        }
    }
}
