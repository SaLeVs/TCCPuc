using System;
using Missions.Donations;
using Unity.Services.Vivox;
using UnityEngine;

namespace Audio
{
    public class DonationTTSManager : MonoBehaviour
    {
        [Header("TTS")]
        [SerializeField] private bool enableTTS = true;

        [SerializeField]
        [Tooltip("Vivox TTS voice name. Leave empty for the default. Available names are logged on login")]
        private string ttsVoice = "en_US female";

        private DonationManager _donationManager;

        private void Start()
        {
            TrySubscribe();

            if (VivoxService.Instance == null) return;

            if (VivoxService.Instance.IsLoggedIn) ApplyVoice();
            else VivoxService.Instance.LoggedIn += ApplyVoice;
        }

        /// <summary>
        /// Vivox only exposes its voices once logged in, and has no setting for them outside code.
        /// The list is logged so the name for <see cref="ttsVoice"/> can be copied from the Console.
        /// </summary>
        private void ApplyVoice()
        {
            VivoxService.Instance.LoggedIn -= ApplyVoice;

            Debug.Log($"DonationAudioManager: available TTS voices: {string.Join(", ", VivoxService.Instance.TextToSpeechAvailableVoices)}");

            if (string.IsNullOrWhiteSpace(ttsVoice)) return;

            VivoxService.Instance.TextToSpeechSetVoice(ttsVoice.Trim());
        }

        private void TrySubscribe()
        {
            _donationManager = DonationManager.Instance;

            if (_donationManager == null)
            {
                Debug.LogWarning("DonationAudioManager: DonationManager not found."); return;
            }
            
            _donationManager.OnDonationSpawned += DonationManager_OnDonationSpawned;
        }

        
        private void DonationManager_OnDonationSpawned(DonationInstance donation)
        {
            if (!enableTTS) return;
            if (donation == null) return;

            SpeakDonation(donation);
        }

        private void SpeakDonation(DonationInstance donation)
        {
            string message = donation.Definition != null ? donation.Definition.message : string.Empty;

            string donationText = $"{donation.DonorName} donated R$ {donation.Amount:0.00} to the chat!";
            
            Speak(donationText);
            Speak(message);
        }

        private void Speak(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (VivoxService.Instance == null)
            {
                Debug.LogWarning("DonationAudioManager: VivoxService not found.");
                return;
            }

            if (!VivoxService.Instance.IsLoggedIn)
            {
                Debug.LogWarning("DonationAudioManager: Vivox is not logged in.");
                return;
            }

            try
            {
                VivoxService.Instance.TextToSpeechSendMessage(message, TextToSpeechMessageType.QueuedRemoteTransmissionWithLocalPlayback);

                Debug.Log($"DonationAudioManager: TTS: {message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"DonationAudioManager: Failed to send TTS: {e.Message}");
            }
        }
        
        
        private void OnDisable()
        {
            if (VivoxService.Instance != null) VivoxService.Instance.LoggedIn -= ApplyVoice;

            if (DonationManager.Instance == null) return;

            DonationManager.Instance.OnDonationSpawned -= DonationManager_OnDonationSpawned;
        }
        
    }
}
