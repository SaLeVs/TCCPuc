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
        [SerializeField, Min(0.01f)] private float rangeReleaseSeconds = 2.5f;

        [Tooltip("Seconds the range stays at least at the speech range after the last word, so the " +
                 "pauses between sentences don't shrink it towards a whisper.")]
        [SerializeField, Min(0f)] private float rangeHoldSeconds = 1.5f;

        private VivoxParticipant _participant;
        private string _participantChannel;
        private string _participantPlayerId;
        private float _lastSpeechTime = float.NegativeInfinity;
        private float[] _heardSamples;

        // Long enough to hold a syllable, short enough to follow it — about what Vivox averages
        // its own AudioEnergy over at five updates a second.
        private const float HEARD_WINDOW_SECONDS = 0.05f;
        private AudioReverbFilter _reverbFilter;
        private AudioSource _audioSource;
        private VoiceGain _gain;
        private AudioListener _listener;
        private NetworkObject _owner;
        private float _currentRange;


        private void OnEnable()
        {
            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel += VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;
                VivoxManager.instance.OnLeftChannel += VivoxManager_OnLeftChannel;

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
            if (_participant == participant) return;

            // A new object for the same player: a rejoin or a channel switch. The one held here may
            // be dead already — Vivox cleans up a channel this machine leaves without saying so — and
            // keeping it meant never tapping this voice again.
            if (_participant != null) DestroyTap();

            _participant = participant;
            _participantChannel = participant.ChannelName;
            _participantPlayerId = participant.PlayerId;

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
            // The voice profile's distance law, as a custom curve Steam Audio follows: the level the
            // voice was spoken at inside the near field, then a set number of dB less per doubling.
            // Where it stops being heard is not up to the curve — see ApplyRange.
            _audioSource.rolloffMode = AudioRolloffMode.Custom;
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

            if (voiceProfile != null)
            {
                _audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, voiceProfile.BuildRolloff(_audioSource.maxDistance));
            }
            else
            {
                _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }

            // Added after the tap, so it scales the voice the tap has just written.
            _gain = _audioSource.gameObject.AddComponent<VoiceGain>();

            // Open at the quiet end, so the first word someone says widens the range instead of
            // the range starting wide and audibly shrinking around them.
            _currentRange = rangeScalesWithVoice ? QuietRange() : LoudRange();
            ApplyRange(IsReadingDonation());

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
        /// <para>The energy is measured locally from the stream this machine is receiving (see
        /// HeardEnergy): Vivox keeps no AudioEnergy for a tapped participant. That costs no RPC, and
        /// every listener derives the same range from the same stream.</para>
        /// </summary>
        private void Update()
        {
            if (_participant == null || _audioSource == null) return;

            bool reading = IsReadingDonation();
            float energy;

            try
            {
                // Vivox leaves AudioEnergy at 0 for a tapped participant (measured 2026-11-01: the
                // voice played, the energy never moved), so the loudness is read from the tap itself.
                energy = Mathf.Max((float)_participant.AudioEnergy, HeardEnergy());
            }
            catch (System.NullReferenceException)
            {
                // The participant can go invalid between leaving the channel and this frame.
                return;
            }

            // The name tag's voice icon reads it from here: it has no tap of its own to measure.
            if (VivoxManager.instance != null) VivoxManager.instance.SetHeardEnergy(_participantPlayerId, energy);

            if (reading)
            {
                // The TTS rides this voice but not its mic level: a donation being read carries to
                // everyone in the channel's range, like a shout, for as long as the reading lasts.
                _currentRange = LoudRange();
            }
            else if (rangeScalesWithVoice)
            {
                float quiet = QuietRange();
                float loud = LoudRange();
                float target = RangeFor(energy, quiet, loud);

                // Asymmetric envelope: rise fast, fall slow. Both rates are expressed as seconds to
                // cross the whole span, so the tuning keeps its meaning when the span itself changes.
                float seconds = target > _currentRange ? rangeAttackSeconds : rangeReleaseSeconds;
                float rate = Mathf.Max(loud - quiet, 0.01f) / seconds;

                _currentRange = Mathf.MoveTowards(_currentRange, target, rate * Time.deltaTime);
            }

            ApplyRange(reading);
        }

        /// <summary>
        /// Talking keeps the range at the speech range at least — through the pauses too, for the hold
        /// time — and raising the voice lifts it towards the shout range. Only someone who stopped
        /// talking, or whispers, falls back to the whisper range. Following each syllable from the
        /// whisper range up, as before, left normal talking unheard 40% of the time at 11 m.
        /// </summary>
        private float RangeFor(float energy, float quiet, float loud)
        {
            if (voiceProfile == null) return loud;

            if (energy >= voiceProfile.SpeechEnergy)
            {
                _lastSpeechTime = Time.time;
                return Mathf.Lerp(SpeechRange(), loud, voiceProfile.Loudness(energy));
            }

            return Time.time - _lastSpeechTime <= rangeHoldSeconds ? SpeechRange() : quiet;
        }

        /// <summary>
        /// Fades the voice to exactly zero at the current range, through the source's volume: there is
        /// one listener per machine, so the distance to it is all a curve would have known anyway, and
        /// the rolloff stays fixed instead of being rebuilt every time the range moves.
        ///
        /// <para>Only the last stretch of the range closes: up to the profile's Edge Fade Start the
        /// distance law alone sets the level, and a cosine takes it from there to silence. The
        /// (1 - x²)² window used before acted over the whole range and already took 8 dB at 60% of it.</para>
        /// </summary>
        private void ApplyRange(bool reading)
        {
            float window = 0f;

            if (TryGetListener(out AudioListener listener))
            {
                float x = Vector3.Distance(listener.transform.position, _audioSource.transform.position) /
                          Mathf.Max(_currentRange, 0.01f);
                float fadeStart = voiceProfile != null ? voiceProfile.EdgeFadeStart : 0f;

                if (x <= fadeStart)
                {
                    window = 1f;
                }
                else if (x < 1f)
                {
                    window = 0.5f * (1f + Mathf.Cos(Mathf.PI * (x - fadeStart) / (1f - fadeStart)));
                }
            }

            float playerVolume = VivoxManager.instance != null
                ? VivoxManager.instance.GetParticipantTapVolume(_participant.PlayerId)
                : 1f;

            // While a donation is read this voice is the reading, played at the volume this player
            // chose for readings — 0 with them turned off.
            if (reading) playerVolume *= VoiceChatSettings.DonationTtsGain;

            // The distance fade stays on the source; the volume this player chose, which can go above
            // 1, rides the gain — AudioSource.volume would cap a boost at 1.
            if (_gain != null)
            {
                _audioSource.volume = window;
                _gain.Gain = playerVolume;
            }
            else
            {
                _audioSource.volume = playerVolume * window;
            }
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

        /// <summary>
        /// The voice's level as it arrives, on Vivox's AudioEnergy scale. The tap plays a looping clip
        /// that Vivox fills just ahead of the play head and clears behind it, so the stretch right
        /// after the play head is the voice about to be heard — before distance, gain and walls.
        /// </summary>
        private float HeardEnergy()
        {
            AudioClip clip = _audioSource.clip;
            if (clip == null || voiceProfile == null) return 0f;

            int frames = Mathf.Clamp(Mathf.RoundToInt(clip.frequency * HEARD_WINDOW_SECONDS), 64, clip.samples);
            int count = frames * clip.channels;

            if (_heardSamples == null || _heardSamples.Length != count) _heardSamples = new float[count];

            // At the wrap the window is pulled back a little rather than split in two reads.
            int start = Mathf.Min(_audioSource.timeSamples, clip.samples - frames);
            if (!clip.GetData(_heardSamples, start)) return 0f;

            double power = 0;
            foreach (float sample in _heardSamples) power += sample * sample;
            power /= count;

            if (power <= 1e-12) return 0f;

            return voiceProfile.EnergyFromHeardLevel(10f * Mathf.Log10((float)power));
        }

        /// <summary>How far normal talking carries: the profile's speech range, between the other two.</summary>
        private float SpeechRange()
        {
            float quiet = QuietRange();
            float loud = LoudRange();
            float speech = voiceProfile != null ? voiceProfile.SpeechRange : loud;

            return Mathf.Clamp(speech, quiet, loud);
        }

        private void VivoxManager_OnParticipantLeft(VivoxParticipant participant)
        {
            // By reference: the one held here may already be dead, and reading its PlayerId throws.
            if (_participant == null || participant != _participant) return;

            DestroyTap();
        }

        private void VivoxManager_OnLeftChannel(string channelName)
        {
            if (_participant == null || _participantChannel != channelName) return;

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

            if (VivoxManager.instance != null) VivoxManager.instance.SetHeardEnergy(_participantPlayerId, 0f);

            _participant = null;
            _participantChannel = null;
            _participantPlayerId = null;
            _reverbFilter = null;
            _gain = null;
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
                VivoxManager.instance.OnLeftChannel -= VivoxManager_OnLeftChannel;
            }

            if (zoneState != null)
            {
                zoneState.OnZonePresetChanged -= PlayerZoneAudioState_OnZonePresetChanged;
            }

            DestroyTap();
        }

    }
}
