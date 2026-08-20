using Network;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerInfos : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
               UserData userData = HostSingleton.instance.gameManager.NetworkServer.GetUserDataByClient(OwnerClientId);
               PlayerName.Value = userData.playerName;
            }
            
        }
        
    } 
}

