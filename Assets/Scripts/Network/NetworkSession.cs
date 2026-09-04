using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public static class NetworkSession
    {
        private const float ShutdownTimeoutSeconds = 5f;

        private static int _transitionDepth;

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
        /// True while we are deliberately tearing a session down to start another one.
        ///
        /// NGO raises OnClientDisconnectCallback from inside its own shutdown (ShutdownInternal
        /// invokes it with LocalClientId when we are the host), so it looks identical to losing
        /// the connection. Listeners that send the player back to the menu on a disconnect must
        /// ignore the callback while this is set, or restarting a session bounces to the menu.
        /// </summary>
        public static bool IsTransitioning => _transitionDepth > 0;

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

            _transitionDepth++;

            try
            {
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

                // One extra frame: the teardown's disconnect callbacks must run while
                // IsTransitioning is still set, otherwise they act on our own shutdown.
                await Task.Yield();
            }
            finally
            {
                _transitionDepth--;
            }
        }
        
        public static string DescribeState()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return "NetworkManager.Singleton is null";

            return $"IsListening={nm.IsListening}, ShutdownInProgress={nm.ShutdownInProgress}, " +
                   $"IsServer={nm.IsServer}, IsClient={nm.IsClient}, transport={nm.NetworkConfig?.NetworkTransport?.GetType().Name ?? "null"}";
        }
    }
}
