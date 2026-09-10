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

        [Tooltip("How far a barely-audible voice carries, in meters.")]
        [SerializeField, Min(0f)] private float whisperLoudness = 6f;

        [Tooltip("How far a shout carries, in meters.")]
        [SerializeField, Min(0f)] private float shoutLoudness = 22f;

        [Tooltip("Seconds between voice noise reports while talking. Keeps RPC traffic sane.")]
        [SerializeField, Min(0.05f)] private float voiceReportInterval = 0.35f;

        private IMicSpeechReporter _micWatcher;
        private VivoxParticipant _localParticipant;
        private bool _isReportingSpeech;
        private float _nextVoiceReportTime;

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
                bool isSpeaking = energy >= audioEnergyThreshold;

                if (isSpeaking != _isReportingSpeech)
                {
                    _isReportingSpeech = isSpeaking;
                    NotifySpeakingServerRpc(isSpeaking);
                }

                if (isSpeaking) ReportVoiceNoise(energy);
            }
            catch (System.NullReferenceException)
            {
                Debug.LogWarning("Vivox participant became invalid. Waiting for rebind.");
                _localParticipant = null;
            }
        }

        /// <summary>
        /// Talking is a continuous noise, not an on/off flag, so it is sampled on an interval
        /// and graded by mic energy: whispering is survivable, shouting is not.
        /// </summary>
        private void ReportVoiceNoise(float energy)
        {
            if (!voiceMakesNoise) return;
            if (Time.time < _nextVoiceReportTime) return;

            _nextVoiceReportTime = Time.time + voiceReportInterval;

            // Remap energy from "just loud enough to register" .. "full scale" onto the
            // whisper..shout range, so the threshold itself never produces a 0m noise.
            float t = Mathf.InverseLerp(audioEnergyThreshold, 1f, energy);
            float loudness = Mathf.Lerp(whisperLoudness, shoutLoudness, t);

            ReportVoiceNoiseServerRpc(loudness);
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