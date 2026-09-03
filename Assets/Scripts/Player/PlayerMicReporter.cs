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

        private IMicSpeechReporter _micWatcher;
        private VivoxParticipant _localParticipant;
        private bool _isReportingSpeech;

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
                bool isSpeaking = _localParticipant.AudioEnergy >= audioEnergyThreshold;

                if (isSpeaking == _isReportingSpeech) return;

                _isReportingSpeech = isSpeaking;
                NotifySpeakingServerRpc(isSpeaking);
            }
            catch (System.NullReferenceException)
            {
                Debug.LogWarning("Vivox participant became invalid. Waiting for rebind.");
                _localParticipant = null;
            }
        }

        [Rpc(SendTo.Server)]
        private void NotifySpeakingServerRpc(bool isSpeaking)
        {
            _micWatcher?.NotifySpeaking(isSpeaking);
        }
    }
}