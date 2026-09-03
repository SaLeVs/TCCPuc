using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// Guarantees a clean NetworkManager before a new Host/Client is started.
    ///
    /// NGO's StartHost/StartClient return false the moment NetworkManager.IsListening is
    /// already true (or a shutdown is still in progress). The menu flow here can leave a
    /// previous session listening — you go back from a lobby, or a connect attempt half
    /// succeeds — so the next attempt fails synchronously and, before this, silently.
    /// </summary>
    public static class NetworkSession
    {
        private const float ShutdownTimeoutSeconds = 5f;

        /// <summary>True while a session is up or tearing down.</summary>
        public static bool IsBusy
        {
            get
            {
                NetworkManager nm = NetworkManager.Singleton;
                return nm != null && (nm.IsListening || nm.ShutdownInProgress);
            }
        }

        /// <summary>
        /// Shuts down any running session and waits until NGO has fully torn it down.
        /// Safe to call when nothing is running (returns immediately).
        /// </summary>
        public static async Task EnsureStoppedAsync()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (!nm.IsListening && !nm.ShutdownInProgress) return;

            Debug.Log("NetworkSession: a session was still running — shutting it down before starting a new one.");
            nm.Shutdown();

            float deadline = Time.realtimeSinceStartup + ShutdownTimeoutSeconds;

            // NGO needs at least one frame after Shutdown() before it will start again.
            while ((nm.IsListening || nm.ShutdownInProgress) && Time.realtimeSinceStartup < deadline)
            {
                await Task.Yield();
            }

            if (nm.IsListening || nm.ShutdownInProgress)
            {
                Debug.LogWarning("NetworkSession: shutdown did not finish within the timeout; starting anyway.");
            }
        }

        /// <summary>
        /// One-line diagnostic string for logs when a start call fails.
        /// </summary>
        public static string DescribeState()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return "NetworkManager.Singleton is null";

            return $"IsListening={nm.IsListening}, ShutdownInProgress={nm.ShutdownInProgress}, " +
                   $"IsServer={nm.IsServer}, IsClient={nm.IsClient}, transport={nm.NetworkConfig?.NetworkTransport?.GetType().Name ?? "null"}";
        }
    }
}
