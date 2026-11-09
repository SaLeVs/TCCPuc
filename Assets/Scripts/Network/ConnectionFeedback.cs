using System;
using Components;
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

        /// <summary>
        /// Set by a screen that shows connection messages itself, such as the main menu's notice
        /// panels. Returns true when it showed the message, and then the toast is skipped so the
        /// same message does not appear twice.
        /// </summary>
        public static Func<string, bool> Presenter { get; set; }

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

            // Restarting a session tears the old one down on purpose — not a connection failure.
            if (NetworkSession.IsTransitioning) return;

            bool isSelf = !networkManager.IsServer || clientId == networkManager.LocalClientId;
            if (!isSelf) return;

            Report(DescribeDisconnect(networkManager.DisconnectReason));
        }

        /// <summary>
        /// NGO fills DisconnectReason with its own transport log line ("[Disconnect Event][Client-0]...")
        /// when the connection itself failed; that reaches the player in words instead. A reason sent
        /// by the server is shown as it is.
        /// </summary>
        private static string DescribeDisconnect(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason) && reason.Contains("MaxConnectionAttempts"))
            {
                return "Ninguém respondeu nesse IP e porta. Confira os dois, se a partida já foi criada e o firewall do host.";
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.StartsWith("[Disconnect Event]"))
            {
                return "Conexão recusada ou perdida. Verifique o IP, a porta ou o firewall (UDP de entrada).";
            }

            return $"Desconectado: {reason}";
        }

        private void NetworkManager_OnTransportFailure()
        {
            Report(
                "Falha de transporte: o host caiu ou a porta está ocupada. Verifique o IP, a porta ou o firewall (UDP de entrada).");
        }

        public static void Report(string message)
        {
            LastMessage = message;
            Debug.LogWarning($"ConnectionFeedback: {message}");

            if (Presenter == null || !Presenter(message))
            {
                ScreenToast.Show(message, ToastKind.Warning);
            }

            OnMessage?.Invoke(message);
        }

        /// <summary>Domain reload may be off, so a presenter left over from the last play session is cleared by hand.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Presenter = null;
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
