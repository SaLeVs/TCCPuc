using UnityEngine;
using UnityEngine.Audio;

namespace Components.Sound
{
    /// <summary>
    /// Plays sounds on this machine from a fixed pool of AudioSources.
    ///
    /// <para>Nothing here is networked. <see cref="WorldSound"/> decides who hears what, and every
    /// peer reaches this exactly once per sound; this only answers how it sounds.</para>
    ///
    /// <para>Pooled instead of a new GameObject per sound: footsteps from four players and a monster
    /// were allocating and destroying objects on every step, and each fresh Steam Audio source needs
    /// a simulation pass before it knows which walls stand between it and the ear.</para>
    ///
    /// <para>Runs its LateUpdate before Steam Audio's, so a sound following its emitter is simulated
    /// where the emitter is this frame rather than where it was last frame.</para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SoundPlayer : MonoBehaviour
    {
        public static SoundPlayer Instance { get; private set; }

        [Header("Mixer")]
        [SerializeField] private AudioMixerGroup worldGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        [Header("World sounds")]
        [Tooltip("HRTF, air absorption and walls for every world sound.")]
        [SerializeField] private SpatialAudioProfileSO worldProfile;

        [Tooltip("Fraction of a sound's range heard at full volume before the inverse-distance falloff " +
                 "starts. At 0.1 a 10 m sound is full strength within 1 m.")]
        [SerializeField, Range(0.02f, 0.5f)] private float nearField = 0.1f;

        [Tooltip("How long a world sound waits before it starts, in seconds. Steam Audio works out the " +
                 "walls between a source and the ear once a frame, so a source that plays the instant it " +
                 "is moved plays its first milliseconds with the walls of wherever it was before.")]
        [SerializeField, Range(0f, 0.1f)] private float worldStartDelay = 0.04f;

        [Header("Pool")]
        [Tooltip("Sounds that can play at once. Unity mixes 32 real voices; stay under that so music " +
                 "and voice chat keep theirs.")]
        [SerializeField, Range(4, 28)] private int poolSize = 20;

        private Slot[] _slots;
        private AnimationCurve _rolloff;

        private sealed class Slot
        {
            public AudioSource Source;

            // The Steam Audio source. Switched off while idle: an enabled one is simulated every
            // frame whether it is playing or not.
            public Behaviour Spatial;

            public float FreeAt;
            public float StartedAt;
            public int Priority;
            public Transform Follow;
            public Vector3 FollowOffset;

            public bool IsBusy(float now) => now < FreeAt;
        }


        private void Awake()
        {
            Instance = this;

            _rolloff = SoundRolloff.Build(nearField);
            _slots = new Slot[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                _slots[i] = CreateSlot(i);
            }
        }

        private Slot CreateSlot(int index)
        {
            var slotObject = new GameObject($"Sound {index:00}");
            slotObject.transform.SetParent(transform, false);

            var source = slotObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _rolloff);
            source.minDistance = 0.01f;

            Behaviour spatial = worldProfile != null ? worldProfile.AttachTo(source) : null;

            if (spatial != null)
            {
                spatial.enabled = false;
            }

            return new Slot { Source = source, Spatial = spatial };
        }

        /// <summary>Plays out in the world: 3D, through walls, heard up to the sound's range.</summary>
        /// <param name="loudness">Multiplies the range — crouched footsteps carry less.</param>
        /// <param name="emitter">Followed while it plays, if the sound asks for that.</param>
        public void PlayWorld(SoundDefinitionSO sound, Vector3 position, float loudness, Transform emitter)
        {
            AudioClip clip = sound.PickClip();
            if (clip == null) return;

            float now = Time.unscaledTime;
            Slot slot = Rent(sound.Priority, now);
            if (slot == null) return;

            AudioSource source = Prepare(slot, sound, clip, worldGroup);
            source.spatialBlend = 1f;
            source.spatialize = slot.Spatial != null;
            source.maxDistance = Mathf.Max(0.1f, sound.Range * loudness);

            Vector3 point = position + Vector3.up * sound.HeightOffset;
            source.transform.position = point;

            slot.Follow = sound.FollowEmitter ? emitter : null;
            slot.FollowOffset = slot.Follow != null ? point - slot.Follow.position : Vector3.zero;

            if (slot.Spatial != null)
            {
                slot.Spatial.enabled = true;
            }

            // Long enough for at least two frames at a low framerate: one for Steam Audio to simulate
            // the new position, one for the source to hand the result to the audio thread.
            float delay = Mathf.Min(0.1f, Mathf.Max(worldStartDelay, Time.unscaledDeltaTime * 2.2f));
            source.PlayDelayed(delay);

            Occupy(slot, sound, clip, source.pitch, now, delay);
        }

        /// <summary>Plays flat in this player's ears: HUD feedback, heard by nobody else.</summary>
        public void PlayUi(SoundDefinitionSO sound)
        {
            AudioClip clip = sound.PickClip();
            if (clip == null) return;

            float now = Time.unscaledTime;
            Slot slot = Rent(sound.Priority, now);
            if (slot == null) return;

            AudioSource source = Prepare(slot, sound, clip, uiGroup);
            source.spatialBlend = 0f;
            source.spatialize = false;
            slot.Follow = null;

            if (slot.Spatial != null)
            {
                slot.Spatial.enabled = false;
            }

            source.Play();

            Occupy(slot, sound, clip, source.pitch, now, 0f);
        }

        private static AudioSource Prepare(Slot slot, SoundDefinitionSO sound, AudioClip clip, AudioMixerGroup group)
        {
            AudioSource source = slot.Source;
            source.Stop();
            source.clip = clip;
            source.volume = sound.Volume;
            source.pitch = sound.PickPitch();
            source.priority = sound.Priority;
            source.outputAudioMixerGroup = group;
            return source;
        }

        private static void Occupy(Slot slot, SoundDefinitionSO sound, AudioClip clip, float pitch, float now, float delay)
        {
            slot.Priority = sound.Priority;
            slot.StartedAt = now;
            slot.FreeAt = now + delay + clip.length / Mathf.Max(0.05f, Mathf.Abs(pitch)) + 0.05f;
        }

        /// <summary>
        /// A free slot, or else the least important sound playing — the oldest among equals — as long
        /// as it matters no more than the new one. A footstep never cuts off a roar.
        /// </summary>
        private Slot Rent(int priority, float now)
        {
            Slot victim = null;

            foreach (Slot slot in _slots)
            {
                if (!slot.IsBusy(now)) return slot;

                bool lessImportant = victim == null
                                     || slot.Priority > victim.Priority
                                     || (slot.Priority == victim.Priority && slot.StartedAt < victim.StartedAt);

                if (lessImportant) victim = slot;
            }

            return victim != null && victim.Priority >= priority ? victim : null;
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;

            foreach (Slot slot in _slots)
            {
                if (!slot.IsBusy(now))
                {
                    if (slot.Spatial != null && slot.Spatial.enabled) slot.Spatial.enabled = false;
                    slot.Follow = null;
                    continue;
                }

                // Unity's null check: an emitter destroyed mid-sound just stops being followed.
                if (slot.Follow != null)
                {
                    slot.Source.transform.position = slot.Follow.position + slot.FollowOffset;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
