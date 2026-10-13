using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

namespace Components.Sound
{
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

        [Tooltip("Sounds closer than this to the listener wait a single frame instead of worldStartDelay. " +
                 "No wall fits between the ear and the player's own feet, and a step that sounds 70 ms " +
                 "after the foot lands reads as lag.")]
        [SerializeField, Range(0f, 5f)] private float instantStartDistance = 2.5f;

        [Header("Own body")]
        [Tooltip("Volume of the sounds this player's own body makes — their footsteps, their damage — as " +
                 "they hear them. Played flat in their ears instead of out in the world: at their feet a " +
                 "3D source sits half inside the floor, so Steam Audio muffles it, and the HRTF from right " +
                 "below the head sounds hollow. Everybody else still hears them in 3D.")]
        [SerializeField, Range(0f, 1f)] private float ownBodyVolume = 0.75f;

        [Header("Pool")]
        [Tooltip("Sounds that can play at once. Unity mixes 32 real voices; stay under that so music " +
                 "and voice chat keep theirs.")]
        [SerializeField, Range(4, 28)] private int poolSize = 20;

        private Slot[] _slots;
        private AnimationCurve _rolloff;
        private AudioListener _listener;

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

            if (IsLocalPlayer(emitter))
            {
                PlayOwnBody(slot, sound, clip, loudness, now);
                return;
            }

            AudioSource source = Prepare(slot, sound, clip, worldGroup);
            source.spatialBlend = 1f;
            source.spatialize = slot.Spatial != null;
            source.maxDistance = Mathf.Max(0.1f, sound.Range * loudness);

            slot.Follow = sound.FollowEmitter ? emitter : null;

            // A followed sound is pinned to the emitter as this machine sees it. The position sent with
            // a remote sound is where the owner was, which runs ahead of the interpolated copy here —
            // keeping that offset left remote steps hanging in front of or behind the body.
            slot.FollowOffset = Vector3.up * sound.HeightOffset;

            Vector3 point = slot.Follow != null
                ? slot.Follow.position + slot.FollowOffset
                : position + Vector3.up * sound.HeightOffset;

            source.transform.position = point;

            if (slot.Spatial != null)
            {
                slot.Spatial.enabled = true;
            }

            float delay = StartDelayFor(point);
            source.PlayDelayed(delay);

            Occupy(slot, sound, clip, source.pitch, now, delay);
        }

        /// <summary>
        /// Flat in this player's ears, through the world mixer group so the world volume still applies.
        /// Loudness still shapes it — a crouched step is quieter and a sprint louder — but by its
        /// square root, so crouching stays audible to the one doing it.
        /// </summary>
        private void PlayOwnBody(Slot slot, SoundDefinitionSO sound, AudioClip clip, float loudness, float now)
        {
            AudioSource source = Prepare(slot, sound, clip, worldGroup);
            source.volume = Mathf.Clamp01(sound.Volume * ownBodyVolume * Mathf.Sqrt(Mathf.Max(0f, loudness)));
            source.spatialBlend = 0f;
            source.spatialize = false;
            slot.Follow = null;

            if (slot.Spatial != null)
            {
                slot.Spatial.enabled = false;
            }

            // No Steam Audio to wait for.
            source.Play();

            Occupy(slot, sound, clip, source.pitch, now, 0f);
        }

        private static bool IsLocalPlayer(Transform emitter)
        {
            if (emitter == null) return false;

            NetworkObject networkObject = emitter.GetComponentInParent<NetworkObject>();
            return networkObject != null && networkObject.IsSpawned && networkObject.IsLocalPlayer;
        }

        /// <summary>
        /// Long enough for at least two frames at a low framerate: one for Steam Audio to simulate the
        /// new position, one for the source to hand the result to the audio thread. Right by the ear a
        /// single frame does, since there are no walls to work out.
        /// </summary>
        private float StartDelayFor(Vector3 point)
        {
            float frame = Time.unscaledDeltaTime;

            if (IsNearListener(point))
            {
                return Mathf.Min(worldStartDelay, frame * 1.1f);
            }

            return Mathf.Min(0.1f, Mathf.Max(worldStartDelay, frame * 2.2f));
        }

        private bool IsNearListener(Vector3 point)
        {
            // The listener lives on the local player's camera, which is spawned and destroyed with it.
            if (_listener == null || !_listener.isActiveAndEnabled)
            {
                _listener = FindFirstObjectByType<AudioListener>();
            }

            return _listener != null
                   && (_listener.transform.position - point).sqrMagnitude <= instantStartDistance * instantStartDistance;
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
