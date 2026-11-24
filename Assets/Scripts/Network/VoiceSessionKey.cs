using System;
using Unity.Netcode;

namespace Network
{
    /// <summary>
    /// The name of a match's voice channel when there is no Unity lobby to take it from - a LAN
    /// match. The host makes one up when it starts and hands it to every player through
    /// <see cref="PlayerVoiceIdentity"/>; Vivox only needs everyone to ask for the same name.
    ///
    /// <para>Vivox still runs on its own servers, so this gives LAN matches voice only where the
    /// network lets the Unity services through. Where it does not, the sign-in fails and the
    /// match goes on without voice, as before.</para>
    /// </summary>
    public static class VoiceSessionKey
    {
        private static string _key;
        private static NetworkManager _keyOwner;

        /// <summary>Server-side. The same key for the whole time this machine hosts, a new one each time it starts.</summary>
        public static string ForThisServer(NetworkManager networkManager)
        {
            if (_key != null && _keyOwner == networkManager) return _key;

            _key = Guid.NewGuid().ToString("N");
            _keyOwner = networkManager;

            networkManager.OnServerStopped += NetworkManager_OnServerStopped;
            return _key;
        }

        private static void NetworkManager_OnServerStopped(bool wasHost)
        {
            if (_keyOwner != null) _keyOwner.OnServerStopped -= NetworkManager_OnServerStopped;

            _key = null;
            _keyOwner = null;
        }
    }
}
