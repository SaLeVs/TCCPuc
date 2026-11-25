using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;

namespace Network
{
    public class PlayerVoiceIdentity : NetworkBehaviour
    {
        /// <summary>
        /// The id arrived or changed, on every peer. The owner only writes it once it has spawned, so
        /// everyone else first sees this player with an empty id; whatever pairs the player with its
        /// Vivox participant has to try again when the real one lands, or that voice is never tapped.
        /// </summary>
        public event Action<string> OnVivoxPlayerIdChanged;

        private readonly NetworkVariable<FixedString128Bytes> _vivoxPlayerId =
            new NetworkVariable<FixedString128Bytes>(writePerm: NetworkVariableWritePermission.Owner);

        public string VivoxPlayerId => _vivoxPlayerId.Value.ToString();

        /// <summary>
        /// The match's voice key reached this player (or changed), on every peer. A LAN match takes
        /// its channel name from it; with a Unity lobby it is never needed.
        /// </summary>
        public event Action<string> OnVoiceSessionKeyChanged;

        // Server-written, the same on every player: what a LAN match names its voice channel after.
        private readonly NetworkVariable<FixedString64Bytes> _voiceSessionKey =
            new NetworkVariable<FixedString64Bytes>(writePerm: NetworkVariableWritePermission.Server);

        public string VoiceSessionKey => _voiceSessionKey.Value.ToString();


        public override void OnNetworkSpawn()
        {
            _vivoxPlayerId.OnValueChanged += VivoxPlayerId_OnValueChanged;
            _voiceSessionKey.OnValueChanged += VoiceSessionKey_OnValueChanged;

            if (IsServer)
            {
                _voiceSessionKey.Value = global::Network.VoiceSessionKey.ForThisServer(NetworkManager);
            }

            if (IsOwner)
            {
                _vivoxPlayerId.Value = SafePlayerId();
            }

            if (!_voiceSessionKey.Value.IsEmpty)
            {
                OnVoiceSessionKeyChanged?.Invoke(VoiceSessionKey);
            }

            // Spawned with the value already in it (a late spawn): the pairing gets its chance now.
            if (!_vivoxPlayerId.Value.IsEmpty)
            {
                OnVivoxPlayerIdChanged?.Invoke(VivoxPlayerId);
            }
        }

        public override void OnNetworkDespawn()
        {
            _vivoxPlayerId.OnValueChanged -= VivoxPlayerId_OnValueChanged;
            _voiceSessionKey.OnValueChanged -= VoiceSessionKey_OnValueChanged;
        }

        private void VoiceSessionKey_OnValueChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            OnVoiceSessionKeyChanged?.Invoke(current.ToString());
        }

        private void VivoxPlayerId_OnValueChanged(FixedString128Bytes previous, FixedString128Bytes current)
        {
            OnVivoxPlayerIdChanged?.Invoke(current.ToString());
        }

        /// <summary>
        /// Empty when this machine never signed in (a LAN match on a network that blocks the Unity
        /// services): there is no voice identity then, and reading the service would throw.
        /// </summary>
        private static string SafePlayerId()
        {
            try
            {
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                {
                    return AuthenticationService.Instance.PlayerId ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // Services not initialised.
            }

            return string.Empty;
        }
    }
}
