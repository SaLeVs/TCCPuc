using Components.Sound;
using Missions.Donations;
using Network;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio
{
    public class RemoteVoiceFilter : MonoBehaviour
    {
        [SerializeField] private PlayerVoiceIdentity voiceIdentity;
        [SerializeField] private PlayerZoneAudioState zoneState;

        [Header("Spatial audio")]
        [Tooltip("Where the voice comes out: a point at the mouth, riding the head bone and facing where " +
                 "the character faces. Without it the voice left from the feet.")]
        [SerializeField] private Transform voiceAnchor;

        [SerializeField] private AudioMixerGroup voiceMixerGroup;

        [Tooltip("HRTF, walls and facing for voices.")]
        [SerializeField] private SpatialAudioProfileSO spatialProfile;

        [Header("Range by speaking volume")]
        [Tooltip("Thresholds and distances shared with PlayerMicReporter, so what the other players " +
                 "hear and what the monster hears never disagree.")]
        [SerializeField] private VoiceProfileSO voiceProfile;

        [Tooltip("Off pins the range to the channel's audible distance.")]
        [SerializeField] private bool rangeScalesWithVoice = true;

        [Tooltip("Seconds for the range to cross its whole span while rising. Deliberately short: " +
                 "a shout has to reach the moment it leaves someone's mouth.")]
        [SerializeField, Min(0.01f)] private float rangeAttackSeconds = 0.08f;

        [Tooltip("Seconds for the range to cross its whole span while falling. Deliberately long: " +
                 "without it the range would collapse between syllables and the voice would pump.")]
        [SerializeField, Min(0.01f)] private float rangeReleaseSeconds = 0.9f;

        private VivoxParticipant _participant;
        private AudioReverbFilter _reverbFilter;
        private AudioSource _audioSource;
        private AudioListener _listener;
        private NetworkObject _owner;
        private float _currentRange;


        private void OnEnable()
        {
            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel += VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;

                TryBindExistingParticipant();
            }

            if (zoneState != null)
            {
                zoneState.OnZonePresetChanged += PlayerZoneAudioState_OnZonePresetChanged;
            }
        }


        private void VivoxManager_OnParticipantJoined(VivoxParticipant participant)
        {
            if (participant.IsSelf) return;
            if (voiceIdentity == null) return;
            if (participant.PlayerId != voiceIdentity.VivoxPlayerId) return;
            if (_participant != null) return;

            _participant = participant;

            GameObject tapObject = participant.CreateVivoxParticipantTap($"VoiceTap_{participant.DisplayName}", silenceInChannelAudioMix: true);

            if (tapObject == null)
            {
                _participant = null;
                return;
            }

            tapObject.transform.SetParent(voiceAnchor != null ? voiceAnchor : transform, false);
            tapObject.transform.localPosition = Vector3.zero;
            tapObject.transform.localRotation = Quaternion.identity;

            _audioSource = tapObject.GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                _audioSource = tapObject.GetComponentInChildren<AudioSource>();
            }

            if (_audioSource == null)
            {
                _participant = null;
                return;
            }

            _audioSource.spatialBlend = 1f;
            // Unity's logarithmic rolloff is exactly the inverse-distance law, minDistance/distance:
            // the level the voice was spoken at inside the near field, 6 dB less per doubling past it.
            // Where it stops being heard is not up to the curve — see ApplyRange.
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.outputAudioMixerGroup = voiceMixerGroup;

            if (spatialProfile != null)
            {
                spatialProfile.AttachTo(_audioSource);

                // Vivox starts the tap playing the moment it creates it, before spatialize was on, and
                // Unity only picks up the spatializer when a sound starts. The tap's own drift
                // correction realigns its ring buffer after the restart.
                if (_audioSource.isPlaying)
                {
                    _audioSource.Stop();
                    _audioSource.Play();
                }
            }

            _audioSource.minDistance = voiceProfile != null ? voiceProfile.NearField : 1f;
            _audioSource.maxDistance = LoudRange();

            // Open at the quiet end, so the first word someone says widens the range instead of
            // the range starting wide and audibly shrinking around them.
            _currentRange = rangeScalesWithVoice ? QuietRange() : LoudRange();
            ApplyRange();

            _reverbFilter = _audioSource.gameObject.AddComponent<AudioReverbFilter>();

            AudioReverbPreset initialPreset = zoneState != null ? zoneState.CurrentPreset : AudioReverbPreset.Off;
            _reverbFilter.reverbPreset = initialPreset;
        }

        /// <summary>
        /// Moves the voice's audible range with how loudly the speaker is actually talking.
        ///
        /// <para>With Vivox's gain control off, the level in the stream already is the level it was
        /// spoken at, and the logarithmic rolloff already takes 6 dB off it per doubling of distance
        /// — together that is a real voice. What a digital signal lacks is a point where it drops
        /// below hearing: 1/r never reaches zero. That point is the range, and it depends on the
        /// loudness, just as a shout stays above the noise floor further than a whisper does.</para>
        ///
        /// <para>The energy is read locally from the remote participant, which Vivox already
        /// replicates as participant state. That costs no RPC, and every listener derives the same
        /// range from the stream they are receiving anyway.</para>
        /// </summary>
        private void Update()
        {
            if (_participant == null || _audioSource == null) return;

            if (IsReadingDonation())
            {
                // The TTS rides this voice but not its mic level: a donation being read carries to
                // everyone in the channel's range, like a shout, for as long as the reading lasts.
                _currentRange = LoudRange();
            }
            else if (rangeScalesWithVoice)
            {
                float energy;

                try
                {
                    energy = (float)_participant.AudioEnergy;
                }
                catch (System.NullReferenceException)
                {
                    // The participant can go invalid between leaving the channel and this frame.
                    return;
                }

                float quiet = QuietRange();
                float loud = LoudRange();
                float loudness = voiceProfile != null ? voiceProfile.Loudness(energy) : 1f;
                float target = Mathf.Lerp(quiet, loud, loudness);

                // Asymmetric envelope: rise fast, fall slow. Both rates are expressed as seconds to
                // cross the whole span, so the tuning keeps its meaning when the span itself changes.
                float seconds = target > _currentRange ? rangeAttackSeconds : rangeReleaseSeconds;
                float rate = Mathf.Max(loud - quiet, 0.01f) / seconds;

                _currentRange = Mathf.MoveTowards(_currentRange, target, rate * Time.deltaTime);
            }

            ApplyRange();
        }

        /// <summary>
        /// Fades the voice to exactly zero at the current range, through the source's volume: there is
        /// one listener per machine, so the distance to it is all a curve would have known anyway, and
        /// the rolloff stays fixed instead of being rebuilt every time the range moves.
        ///
        /// <para>(1 - x²)² is the window the world sounds use: it leaves the near and middle distances
        /// almost untouched and closes smoothly on zero instead of cutting the tail off.</para>
        /// </summary>
        private void ApplyRange()
        {
            float window = 0f;

            if (TryGetListener(out AudioListener listener))
            {
                float x = Vector3.Distance(listener.transform.position, _audioSource.transform.position) /
                          Mathf.Max(_currentRange, 0.01f);

                if (x < 1f)
                {
                    window = 1f - x * x;
                    window *= window;
                }
            }

            float playerVolume = VivoxManager.instance != null
                ? VivoxManager.instance.GetParticipantTapVolume(_participant.PlayerId)
                : 1f;

            _audioSource.volume = playerVolume * window;
        }

        private bool IsReadingDonation()
        {
            if (_owner == null) _owner = GetComponentInParent<NetworkObject>();

            return _owner != null && _owner.IsSpawned && DonationReading.IsReading(_owner.OwnerClientId);
        }

        private bool TryGetListener(out AudioListener listener)
        {
            // The listener rides whichever camera is live — the player's own, or a spectator camera
            // after death — so a disabled one is looked up again.
            if (_listener == null || !_listener.isActiveAndEnabled)
            {
                _listener = FindFirstObjectByType<AudioListener>();
            }

            listener = _listener;
            return listener != null;
        }

        /// <summary>
        /// The channel's audible distance is a hard ceiling rather than a suggestion: Vivox stops
        /// delivering a participant's stream past it, so a larger range would only promise audio
        /// that never arrives.
        /// </summary>
        private float LoudRange()
        {
            if (VivoxManager.instance == null) return _audioSource.maxDistance;

            return VivoxManager.instance.AudibleDistance;
        }

        /// <summary>
        /// Held strictly above the near field: a whisper must still fade before it stops.
        /// </summary>
        private float QuietRange()
        {
            float loud = LoudRange();
            float floor = _audioSource.minDistance + 0.5f;

            if (floor >= loud) return loud;

            float whisper = voiceProfile != null ? voiceProfile.WhisperRange : loud;

            return Mathf.Clamp(whisper, floor, loud);
        }

        private void VivoxManager_OnParticipantLeft(VivoxParticipant participant)
        {
            if (_participant == null) return;
            if (participant.PlayerId != _participant.PlayerId) return;

            DestroyTap();
        }

        private void PlayerZoneAudioState_OnZonePresetChanged(AudioReverbPreset preset)
        {
            if (_reverbFilter != null)
            {
                _reverbFilter.reverbPreset = preset;
            }
        }

        private void DestroyTap()
        {
            _participant?.DestroyVivoxParticipantTap();
            _participant = null;
            _reverbFilter = null;
            _audioSource = null;
            _listener = null;
            _currentRange = 0f;
        }

        private void TryBindExistingParticipant()
        {
            if (voiceIdentity == null) return;

            foreach (VivoxParticipant participant in VivoxManager.instance.CurrentParticipants)
            {
                if (participant.PlayerId == voiceIdentity.VivoxPlayerId)
                {
                    VivoxManager_OnParticipantJoined(participant);
                    break;
                }
            }
        }


        private void OnDisable()
        {
            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel -= VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel -= VivoxManager_OnParticipantLeft;
            }

            if (zoneState != null)
            {
                zoneState.OnZonePresetChanged -= PlayerZoneAudioState_OnZonePresetChanged;
            }

            DestroyTap();
        }

    }
}
