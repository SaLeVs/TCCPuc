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


        public override void OnNetworkSpawn()
        {
            _vivoxPlayerId.OnValueChanged += VivoxPlayerId_OnValueChanged;

            if (IsOwner)
            {
                _vivoxPlayerId.Value = SafePlayerId();
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
