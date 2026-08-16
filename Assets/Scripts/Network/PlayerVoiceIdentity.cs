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
            
            _vivoxPlayerId.Value = AuthenticationService.Instance.PlayerId;
        }
        
        
    }
}