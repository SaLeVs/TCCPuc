using System.Collections.Generic;
using Systems;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class PlayersReady : NetworkBehaviour
    {
        [SerializeField] private Loader.Scene sceneToLoad;
        
        private Dictionary<ulong, bool> _playerReadyDictionary;

        
        private void Awake()
        {
            _playerReadyDictionary = new Dictionary<ulong, bool>();
        }

        public void SetPlayerReady()
        {
            SetPlayerReadyServerRpc();
        }
        
        [Rpc(SendTo.Server)]
        private void SetPlayerReadyServerRpc(RpcParams serverParams = default)
        {
            _playerReadyDictionary[serverParams.Receive.SenderClientId] = true;
            
            bool areAllClientsReady = true;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (!_playerReadyDictionary.ContainsKey(clientId) || !_playerReadyDictionary[clientId])
                {
                    areAllClientsReady = false;
                    break;
                }
            }

            if (areAllClientsReady)
            {
                Loader.LoadNetwork(sceneToLoad);
            }
            
        }
    }
}

