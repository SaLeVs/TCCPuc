using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Donations
{
    public class DonationMicWatcher : NetworkBehaviour, IMicSpeechReporter
    {
        [Tooltip("Need to match the micActionId configured in the corresponding DonationDefinition")]
        [SerializeField] private string micActionId = "default";

        private bool _isSpeaking;

        private void Update()
        {
            if (!IsServer) return;
            if (!_isSpeaking) return;

            DonationManager.Instance?.ReportMicSpeech(OwnerClientId, micActionId, Time.deltaTime);
        }

        public void NotifySpeaking(bool isSpeaking)
        {
            if (!IsServer) return;
            _isSpeaking = isSpeaking;
        }
    }
}