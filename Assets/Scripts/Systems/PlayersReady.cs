using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class PlayersReady : NetworkBehaviour
    {
        [SerializeField] private Loader.Scene sceneToLoad;
        
        private Dictionary<ulong, bool> _playerReadyDictionary;

        
        private void Awake()
        {
            _playerReadyDictionary = new Dictionary<ulong, bool>();
        }
        
        [Rpc(SendTo.Server)]
        public void SetPlayerReadyServerRpc(ulong clientId)
        {
            _playerReadyDictionary[clientId] = true;
            Debug.Log($"Player {clientId} is ready");

            bool areAllClientsReady = true;

            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (!_playerReadyDictionary.ContainsKey(id) || !_playerReadyDictionary[id])
                {
                    areAllClientsReady = false;
                    break;
                }
            }

            if (areAllClientsReady)
            {
                Debug.Log("All players are ready!");
                Loader.LoadNetwork(sceneToLoad);
            }
            
        }
    }
}

