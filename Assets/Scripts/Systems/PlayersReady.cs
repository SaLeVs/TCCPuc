using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class PlayersReady : NetworkBehaviour
    {
        public event Action<int, int> OnReadyCountChanged;

        [SerializeField] private Loader.Scene sceneToLoad;

        private readonly Dictionary<ulong, bool> _playerReadyDictionary = new();
        private bool _subscribedToTracker;
        private bool _isSceneLoading;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            BindToPlayerTracker();
            RebuildReadyDictionaryFromTracker();
            CheckAllReady();
        }

        private void BindToPlayerTracker()
        {
            if (_subscribedToTracker || PlayerTracker.Instance == null) return;

            PlayerTracker.Instance.OnPlayerConnected += HandlePlayerConnected;
            PlayerTracker.Instance.OnPlayerDisconnected += HandlePlayerDisconnected;
            _subscribedToTracker = true;
        }

        private void RebuildReadyDictionaryFromTracker()
        {
            _playerReadyDictionary.Clear();

            if (PlayerTracker.Instance == null)
                return;

            foreach (ulong clientId in PlayerTracker.Instance.ConnectedPlayers)
            {
                _playerReadyDictionary[clientId] = false;
            }
        }

        private void HandlePlayerConnected(ulong clientId)
        {
            _playerReadyDictionary[clientId] = false;
            CheckAllReady();
        }

        private void HandlePlayerDisconnected(ulong clientId)
        {
            _playerReadyDictionary.Remove(clientId);
            CheckAllReady();
        }

        public void SetPlayerReadyServer(ulong clientId)
        {
            if (!IsServer) return;

            _playerReadyDictionary[clientId] = true;
            CheckAllReady();
        }

        public void SetPlayerNotReadyServer(ulong clientId)
        {
            if (!IsServer) return;

            _playerReadyDictionary[clientId] = false;
            CheckAllReady();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetPlayerReadyRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            SetPlayerReadyServer(senderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetPlayerNotReadyRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            SetPlayerNotReadyServer(senderClientId);
        }

        private void CheckAllReady()
        {
            if (!IsServer || PlayerTracker.Instance == null)
                return;

            int totalCount = PlayerTracker.Instance.ExpectedPlayerCount;
            int connectedCount = PlayerTracker.Instance.ConnectedPlayerCount;
            int readyCount = 0;

            foreach (ulong id in PlayerTracker.Instance.ConnectedPlayers)
            {
                if (_playerReadyDictionary.TryGetValue(id, out bool isReady) && isReady)
                {
                    readyCount++;
                }
            }

            UpdateReadyCountClientRpc(readyCount, totalCount);

            bool areAllClientsReady = totalCount > 0 && connectedCount >= totalCount && readyCount >= totalCount;

            if (areAllClientsReady && !_isSceneLoading)
            {
                _isSceneLoading = true;
                Debug.Log("All players are ready!");
                Loader.LoadNetwork(sceneToLoad);
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void UpdateReadyCountClientRpc(int readyCount, int totalCount)
        {
            OnReadyCountChanged?.Invoke(readyCount, totalCount);
        }

        public (int ready, int total) GetReadyCount()
        {
            if (PlayerTracker.Instance == null)
                return (0, 0);

            int total = PlayerTracker.Instance.ExpectedPlayerCount;
            int ready = 0;

            foreach (ulong id in PlayerTracker.Instance.ConnectedPlayers)
            {
                if (_playerReadyDictionary.TryGetValue(id, out bool isReady) && isReady)
                {
                    ready++;
                }
            }

            return (ready, total);
        }

        public override void OnNetworkDespawn()
        {
            if (PlayerTracker.Instance != null && _subscribedToTracker)
            {
                PlayerTracker.Instance.OnPlayerConnected -= HandlePlayerConnected;
                PlayerTracker.Instance.OnPlayerDisconnected -= HandlePlayerDisconnected;
                _subscribedToTracker = false;
            }
        }
    }
}