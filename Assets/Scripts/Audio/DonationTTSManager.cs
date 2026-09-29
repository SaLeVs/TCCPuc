using System;
using System.Collections.Generic;
using Missions.Donations;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Reads each donation out loud through the voice of the player it is addressed to.
    ///
    /// <para>Runs on every client, but only the recipient speaks. Vivox's remote transmission puts the
    /// speech into that player's own outgoing voice, so everyone else hears it come from them —
    /// positioned, muffled by walls and fading with distance exactly like their voice — while the
    /// recipient hears it in their headphones. It used to be spoken by the host alone, which made
    /// every donation sound like it came from the host, wherever the host was.</para>
    /// </summary>
    public class DonationTTSManager : MonoBehaviour
    {
        [Header("TTS")]
        [SerializeField] private bool enableTTS = true;

        [SerializeField]
        [Tooltip("Vivox TTS voice name. Leave empty for the default. Available names are logged on login")]
        private string ttsVoice = "en_US female";

        [Tooltip("{donor}, {amount} and {recipient} are filled in. Read before the donation's own message.")]
        [SerializeField] private string announcementFormat = "{donor} donated R$ {amount} to {recipient}!";

        private DonationManager _donationManager;

        // Every donation already considered. Progress updates re-send the whole entry, and each
        // donation must be read once.
        private readonly HashSet<string> _announced = new();

        private void Start()
        {
            if (VivoxService.Instance == null) return;

            if (VivoxService.Instance.IsLoggedIn) ApplyVoice();
            else VivoxService.Instance.LoggedIn += ApplyVoice;
        }

        /// <summary>
        /// The manager spawns through netcode a moment after the scene loads, so keep looking until
        /// it is there.
        /// </summary>
        private void Update()
        {
            if (_donationManager != null) return;

            DonationManager manager = DonationManager.Instance;
            if (manager == null || !manager.IsSpawned) return;

            _donationManager = manager;
            _donationManager.NetworkStates.OnListChanged += DonationManager_OnListChanged;

            foreach (DonationNetworkState state in _donationManager.NetworkStates)
            {
                Consider(state);
            }
        }

        /// <summary>
        /// Vivox only exposes its voices once logged in, and has no setting for them outside code.
        /// </summary>
        private void ApplyVoice()
        {
            VivoxService.Instance.LoggedIn -= ApplyVoice;

            if (string.IsNullOrWhiteSpace(ttsVoice)) return;

            VivoxService.Instance.TextToSpeechSetVoice(ttsVoice.Trim());
        }

        private void DonationManager_OnListChanged(NetworkListEvent<DonationNetworkState> changeEvent)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<DonationNetworkState>.EventType.Add:
                case NetworkListEvent<DonationNetworkState>.EventType.Insert:
                case NetworkListEvent<DonationNetworkState>.EventType.Value:
                    Consider(changeEvent.Value);
                    break;
            }
        }

        private void Consider(DonationNetworkState state)
        {
            if (!enableTTS) return;
            if (state.State != DonationState.Active) return;
            if (!_announced.Add(state.InstanceId.ToString())) return;

            NetworkManager network = NetworkManager.Singleton;
            if (network == null || state.RecipientClientId != network.LocalClientId) return;

            string recipient = state.RecipientName.IsEmpty ? "the chat" : state.RecipientName.ToString();

            string announcement = announcementFormat
                .Replace("{donor}", state.DonorName.ToString())
                .Replace("{amount}", state.Amount.ToString("0.00"))
                .Replace("{recipient}", recipient);

            Speak(announcement);
            Speak(state.Message.ToString());
        }

        private void Speak(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (VivoxService.Instance == null)
            {
                Debug.LogWarning("DonationTTSManager: VivoxService not found.");
                return;
            }

            if (!VivoxService.Instance.IsLoggedIn)
            {
                Debug.LogWarning("DonationTTSManager: Vivox is not logged in.");
                return;
            }

            try
            {
                VivoxService.Instance.TextToSpeechSendMessage(message, TextToSpeechMessageType.QueuedRemoteTransmissionWithLocalPlayback);
            }
            catch (Exception e)
            {
                Debug.LogError($"DonationTTSManager: Failed to send TTS: {e.Message}");
            }
        }

        private void OnDisable()
        {
            if (VivoxService.Instance != null) VivoxService.Instance.LoggedIn -= ApplyVoice;

            if (_donationManager == null) return;

            _donationManager.NetworkStates.OnListChanged -= DonationManager_OnListChanged;
            _donationManager = null;
        }
    }
}
