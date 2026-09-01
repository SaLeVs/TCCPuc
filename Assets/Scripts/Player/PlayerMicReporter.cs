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
            if (!IsOwner) enabled = false;
            
            _micWatcher = micWatcherBehaviour as IMicSpeechReporter;
            
            if (_micWatcher == null)
            {
                Debug.LogError("Failed to cast micWatcherBehaviour to IMicSpeechReporter");
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (_localParticipant == null)
            {
                TryBindLocalParticipant();
                return;
            }

            bool isScreaming = _localParticipant.AudioEnergy >= audioEnergyThreshold;
            if (isScreaming == _isReportingSpeech) return;

            _isReportingSpeech = isScreaming;
            NotifySpeakingServerRpc(isScreaming);
        }

        private void TryBindLocalParticipant()
        {
            if (VivoxManager.instance == null) return;

            foreach (VivoxParticipant participant in VivoxManager.instance.CurrentParticipants)
            {
                if (!participant.IsSelf) continue;
                _localParticipant = participant;
                break;
            }
        }

        [Rpc(SendTo.Server)]
        private void NotifySpeakingServerRpc(bool isSpeaking)
        {
            _micWatcher?.NotifySpeaking(isSpeaking);
        }
    }
}
