using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class ConnectionFeedback : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private float messageDuration = 8f;
        
        public static string LastMessage { get; private set; }

        public static event Action<string> OnMessage;

        private float _hideTimer;
        private bool _subscribed;

        private void OnEnable()
        {
            Subscribe();
            ShowInternal(string.Empty, false);
        }

        private void Update()
        {
            if (!_subscribed) Subscribe();

            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.unscaledDeltaTime;

                if (_hideTimer <= 0f && messageLabel != null)
                {
                    messageLabel.gameObject.SetActive(false);
                }
            }
        }

        private void Subscribe()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || _subscribed) return;

            networkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnect;
            networkManager.OnTransportFailure += NetworkManager_OnTransportFailure;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !_subscribed) return;

            networkManager.OnClientDisconnectCallback -= NetworkManager_OnClientDisconnect;
            networkManager.OnTransportFailure -= NetworkManager_OnTransportFailure;
            _subscribed = false;
        }

        private void NetworkManager_OnClientDisconnect(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null) return;
            
            bool isSelf = !networkManager.IsServer || clientId == networkManager.LocalClientId;
            if (!isSelf) return;

            string reason = networkManager.DisconnectReason;

            Report(string.IsNullOrWhiteSpace(reason)
                ? "Connection denied or lost. Connection denied or lost. Check the IP, the port or the firewall (Entrance UDP)."
                : $"Disconnected: {reason}");
        }

        private void NetworkManager_OnTransportFailure()
        {
            Report(
                "Fail on transport, host down and the port are busy. Fail on transport, host down and the port are busy. Check the IP, the port or the firewall (Entrance UDP).");
        }

        public static void Report(string message)
        {
            LastMessage = message;
            Debug.LogWarning($"ConnectionFeedback: {message}");
            OnMessage?.Invoke(message);
        }

        private void ShowInternal(string message, bool visible)
        {
            if (messageLabel == null) return;

            messageLabel.text = message;
            messageLabel.gameObject.SetActive(visible);
        }

        private void HandleMessage(string message)
        {
            ShowInternal(message, true);
            _hideTimer = messageDuration;
        }

        private void Awake() => OnMessage += HandleMessage;

        private void OnDestroy()
        {
            OnMessage -= HandleMessage;
            Unsubscribe();
        }

        private void OnDisable() => Unsubscribe();
    }
}
