using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;

namespace Network
{
    public class PlayerVoiceIdentity : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString128Bytes> _vivoxPlayerId =
            new NetworkVariable<FixedString128Bytes>(writePerm: NetworkVariableWritePermission.Owner);

        public string VivoxPlayerId => _vivoxPlayerId.Value.ToString();

        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            
            _vivoxPlayerId.Value = SafePlayerId();
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
            catch (System.Exception)
            {
                // Services not initialised.
            }

            return string.Empty;
        }
        
        
    }
}