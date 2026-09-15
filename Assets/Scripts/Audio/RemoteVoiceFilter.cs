using Network;
using Unity.Services.Vivox;
using UnityEngine;

namespace Audio
{
    public class RemoteVoiceFilter : MonoBehaviour
    {
        [SerializeField] private PlayerVoiceIdentity voiceIdentity;
        [SerializeField] private PlayerZoneAudioState zoneState;

        [Header("Range by speaking volume")]
        [Tooltip("Off pins the range to the channel's audible distance, the way it behaved before.")]
        [SerializeField] private bool rangeScalesWithVoice = true;

        [Tooltip("Mic energy at or below which the voice counts as a whisper and only reaches " +
                 "whisperDistance. Mirrors PlayerMicReporter.speechEnergyThreshold on purpose: the " +
                 "moment the monster starts hearing you is the moment your voice starts carrying.")]
        [SerializeField, Range(0f, 1f)] private float speechEnergyThreshold = 0.25f;

        [Tooltip("Mic energy at or above which it counts as a shout and reaches the channel limit. " +
                 "Mirrors PlayerMicReporter.shoutEnergyThreshold.")]
        [SerializeField, Range(0f, 1f)] private float shoutEnergyThreshold = 0.65f;

        [Tooltip("How far a whisper carries, in meters.")]
        [SerializeField, Min(0f)] private float whisperDistance = 8f;

        [Tooltip("Seconds for the range to cross its whole span while rising. Deliberately short: " +
                 "a shout has to reach the moment it leaves someone's mouth.")]
        [SerializeField, Min(0.01f)] private float rangeAttackSeconds = 0.08f;

        [Tooltip("Seconds for the range to cross its whole span while falling. Deliberately long: " +
                 "without it the range would collapse between syllables and the voice would pump.")]
        [SerializeField, Min(0.01f)] private float rangeReleaseSeconds = 0.9f;

        private VivoxParticipant _participant;
        private AudioReverbFilter _reverbFilter;
        private AudioSource _audioSource;
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

            tapObject.transform.SetParent(transform, false);
            tapObject.transform.localPosition = Vector3.zero;

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
            _audioSource.rolloffMode = AudioRolloffMode.Linear;

            if (VivoxManager.instance != null)
            {
                _audioSource.minDistance = VivoxManager.instance.ConversationalDistance;
                _audioSource.maxDistance = VivoxManager.instance.AudibleDistance;
            }

            if (rangeScalesWithVoice)
            {
                // Open at the quiet end, so the first word someone says widens the range instead of
                // the range starting wide and audibly shrinking around them.
                _currentRange = QuietRange();
                _audioSource.maxDistance = _currentRange;
            }

            _reverbFilter = _audioSource.gameObject.AddComponent<AudioReverbFilter>();

            AudioReverbPreset initialPreset = zoneState != null ? zoneState.CurrentPreset : AudioReverbPreset.Off;
            _reverbFilter.reverbPreset = initialPreset;
        }

        /// <summary>
        /// Moves the tap's audible range with how loudly the speaker is actually talking.
        ///
        /// <para>Under linear rolloff maxDistance is not only where a voice stops being audible, it
        /// also sets how fast it fades on the way out — so driving it from mic energy does both
        /// halves of the job at once: a whisper is quieter at 6 m <i>and</i> gone by 8 m, while a
        /// shout stays strong all the way to the channel limit.</para>
        ///
        /// <para>The energy is read locally from the remote participant, which Vivox already
        /// replicates as participant state. That costs no RPC, and every listener derives the same
        /// curve from the stream they are receiving anyway.</para>
        /// </summary>
        private void Update()
        {
            if (!rangeScalesWithVoice) return;
            if (_participant == null || _audioSource == null) return;

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
            float target = Mathf.Lerp(quiet, loud, Mathf.InverseLerp(speechEnergyThreshold, shoutEnergyThreshold, energy));

            // Asymmetric envelope: rise fast, fall slow. Both rates are expressed as seconds to
            // cross the whole span, so the tuning keeps its meaning when the span itself changes.
            float seconds = target > _currentRange ? rangeAttackSeconds : rangeReleaseSeconds;
            float rate = Mathf.Max(loud - quiet, 0.01f) / seconds;

            _currentRange = Mathf.MoveTowards(_currentRange, target, rate * Time.deltaTime);
            _audioSource.maxDistance = _currentRange;
        }

        /// <summary>
        /// The channel's audible distance is a hard ceiling rather than a suggestion: Vivox stops
        /// delivering a participant's stream past it, so a larger maxDistance would only promise
        /// audio that never arrives.
        /// </summary>
        private float LoudRange()
        {
            if (VivoxManager.instance == null) return _audioSource.maxDistance;

            return VivoxManager.instance.AudibleDistance;
        }

        /// <summary>
        /// Held strictly above minDistance. That one is the full-volume radius and must not move
        /// with speaking volume, and linear rolloff degenerates when the two meet.
        /// </summary>
        private float QuietRange()
        {
            float loud = LoudRange();
            float floor = _audioSource.minDistance + 0.5f;

            if (floor >= loud) return loud;

            return Mathf.Clamp(whisperDistance, floor, loud);
        }

        private void OnValidate()
        {
            // Mathf.InverseLerp silently inverts when these cross, which would make speaking
            // quietly the thing that carries furthest.
            shoutEnergyThreshold = Mathf.Max(shoutEnergyThreshold, speechEnergyThreshold);
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
