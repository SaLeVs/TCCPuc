using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System;

namespace Systems
{
    public class PlayersReady : NetworkBehaviour
    {
        public event Action<int, int> OnReadyCountChanged;
        
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
            CheckAllReady();
        }

        [Rpc(SendTo.Server)]
        public void SetPlayerNotReadyServerRpc(ulong clientId)
        {
            _playerReadyDictionary[clientId] = false;
            CheckAllReady();
        }

        private void CheckAllReady()
        {
            bool areAllClientsReady = true;
            int readyCount = 0;
            int totalCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (_playerReadyDictionary.ContainsKey(id) && _playerReadyDictionary[id])
                {
                    readyCount++; 
                }
                else
                {
                    areAllClientsReady = false;
                }
            }

            UpdateReadyCountClientRpc(readyCount, totalCount);

            if (areAllClientsReady)
            {
                Debug.Log("All players are ready!");
                Loader.LoadNetwork(sceneToLoad);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void UpdateReadyCountClientRpc(int readyCount, int totalCount)
        {
            OnReadyCountChanged?.Invoke(readyCount, totalCount);
        }
    }
}

