using Components.Perception;
using Interfaces;
using Network;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace Player
{
    public class PlayerMicReporter : NetworkBehaviour
    {
        [SerializeField] private MonoBehaviour micWatcherBehaviour;
        [SerializeField] private float audioEnergyThreshold = 0.4f;

        [Header("Noise heard by the monster")]
        [Tooltip("Off to make this player's voice inaudible to the AI.")]
        [SerializeField] private bool voiceMakesNoise = true;

        [Tooltip("Mic energy above which normal talking starts making noise. Below it the player " +
                 "is effectively whispering and the monster hears nothing.")]
        [SerializeField, Range(0f, 1f)] private float speechEnergyThreshold = 0.35f;

        [Tooltip("Mic energy above which it counts as a shout instead of talking.")]
        [SerializeField, Range(0f, 1f)] private float shoutEnergyThreshold = 0.72f;

        [Tooltip("How far normal talking carries, in meters.")]
        [SerializeField, Min(0f)] private float speechLoudness = 10f;

        [Tooltip("How far a shout carries, in meters.")]
        [SerializeField, Min(0f)] private float shoutLoudness = 24f;

        [Tooltip("Seconds between voice noise reports while talking. Keeps RPC traffic sane.")]
        [SerializeField, Min(0.05f)] private float voiceReportInterval = 0.35f;

        private IMicSpeechReporter _micWatcher;
        private VivoxParticipant _localParticipant;
        private bool _isReportingSpeech;
        private float _nextVoiceReportTime;
        private VoiceTier _lastReportedTier = VoiceTier.Silent;

        private enum VoiceTier
        {
            Silent,
            Speech,
            Shout
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _micWatcher = micWatcherBehaviour as IMicSpeechReporter;

            if (_micWatcher == null)
            {
                Debug.LogError("Failed to cast micWatcherBehaviour to IMicSpeechReporter");
                return;
            }

            SubscribeVivoxEvents();
            BindExistingParticipant();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            UnsubscribeVivoxEvents();
        }

        private void SubscribeVivoxEvents()
        {
            if (VivoxManager.instance == null) return;

            VivoxManager.instance.OnParticipantJoinedChannel += OnParticipantAdded;
            VivoxManager.instance.OnParticipantLeftChannel += OnParticipantRemoved;
        }

        private void UnsubscribeVivoxEvents()
        {
            if (VivoxManager.instance == null) return;

            VivoxManager.instance.OnParticipantJoinedChannel -= OnParticipantAdded;
            VivoxManager.instance.OnParticipantLeftChannel -= OnParticipantRemoved;
        }

        private void BindExistingParticipant()
        {
            if (VivoxManager.instance == null) return;

            foreach (var participant in VivoxManager.instance.CurrentParticipants)
            {
                if (!participant.IsSelf) continue;

                _localParticipant = participant;
                break;
            }
        }

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            if (!participant.IsSelf) return;
            
            _localParticipant = participant;
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            if (_localParticipant != participant) return;

            _localParticipant = null;
            _isReportingSpeech = false;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_localParticipant == null) return;

            try
            {
                float energy = (float)_localParticipant.AudioEnergy;

                // Two independent gates on the same signal. audioEnergyThreshold answers "is this
                // player talking?" for the donation watcher; the noise tiers answer "how much of
                // this can the monster hear?". Routing the noise through the speaking flag meant
                // normal talking never reported, because that flag sits far higher.
                bool isSpeaking = energy >= audioEnergyThreshold;

                if (isSpeaking != _isReportingSpeech)
                {
                    _isReportingSpeech = isSpeaking;
                    NotifySpeakingServerRpc(isSpeaking);
                }

                ReportVoiceNoise(energy);
            }
            catch (System.NullReferenceException)
            {
                Debug.LogWarning("Vivox participant became invalid. Waiting for rebind.");
                _localParticipant = null;
            }
        }

        /// <summary>
        /// Talking is a continuous noise, so it is sampled on an interval — but it reports in two
        /// discrete tiers rather than a smooth ramp, because the two mean different things to the
        /// player: talking is the price of coordinating, shouting is a decision.
        /// </summary>
        private void ReportVoiceNoise(float energy)
        {
            if (!voiceMakesNoise) return;

            VoiceTier tier = TierFor(energy);

            if (tier == VoiceTier.Silent)
            {
                _lastReportedTier = VoiceTier.Silent;
                return;
            }

            // A scream that lands right after a routine speech report should not be swallowed by
            // the throttle — escalating tier reports immediately.
            bool escalated = tier == VoiceTier.Shout && _lastReportedTier != VoiceTier.Shout;

            if (!escalated && Time.time < _nextVoiceReportTime) return;

            _nextVoiceReportTime = Time.time + voiceReportInterval;
            _lastReportedTier = tier;

            float loudness = tier == VoiceTier.Shout ? shoutLoudness : speechLoudness;

            ReportVoiceNoiseServerRpc(loudness);
        }

        private VoiceTier TierFor(float energy)
        {
            if (energy >= shoutEnergyThreshold) return VoiceTier.Shout;
            if (energy >= speechEnergyThreshold) return VoiceTier.Speech;

            return VoiceTier.Silent;
        }

        [Rpc(SendTo.Server)]
        private void NotifySpeakingServerRpc(bool isSpeaking)
        {
            _micWatcher?.NotifySpeaking(isSpeaking);
        }

        [Rpc(SendTo.Server)]
        private void ReportVoiceNoiseServerRpc(float loudness)
        {
            // Position comes from the server's copy of this player, not from the client —
            // a client can only tell us how loud it was, never where it was.
            NoiseBus.Report(transform.position, loudness, NoiseType.Voice, transform.root);
        }
    }
}