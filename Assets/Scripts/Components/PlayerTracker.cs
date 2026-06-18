using System;
using System.Collections.Generic;
using Components;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Systems
{
    public class PlayerTracker : NetworkBehaviour
    {
        public static PlayerTracker Instance { get; private set; }

        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;
        public event Action<int> OnPlayerCountChanged;
        public event Action<int> OnExpectedPlayerCountChanged;
        public event Action OnAllPlayersConnected;

        public IReadOnlyList<ulong> ConnectedPlayers => _connectedPlayers;
        public int ConnectedPlayerCount => _connectedPlayers.Count;
        public int ExpectedPlayerCount => _expectedPlayerCount;
        public bool AreAllPlayersConnected => _expectedPlayerCount > 0 && _connectedPlayers.Count >= _expectedPlayerCount;

        private readonly List<ulong> _connectedPlayers = new();
        private int _expectedPlayerCount = 1;

        private bool _subscribed;
        private bool _wasAllPlayersConnected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            SubscribeToNetworkEvents();
            RebuildConnectedPlayersFromNetwork();

            if (MultiplayerModeManager.IsLan)
            {
                SetExpectedPlayerCount(_connectedPlayers.Count == 0 ? 1 : _connectedPlayers.Count);
            }
        }

        private void SubscribeToNetworkEvents()
        {
            if (_subscribed || NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

            _subscribed = true;
        }

        private void RebuildConnectedPlayersFromNetwork()
        {
            _connectedPlayers.Clear();
            _wasAllPlayersConnected = false;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                RegisterPlayerInternal(clientId);
            }

            RefreshCounts();
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            _connectedPlayers.Clear();
            _wasAllPlayersConnected = false;

            if (_expectedPlayerCount <= 0) return;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                RegisterPlayerInternal(clientId);
            }

            RefreshCounts();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            RegisterPlayerInternal(clientId);

            if (MultiplayerModeManager.IsLan)
            {
                SetExpectedPlayerCount(_connectedPlayers.Count == 0 ? 1 : _connectedPlayers.Count);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (_connectedPlayers.Remove(clientId))
            {
                OnPlayerDisconnected?.Invoke(clientId);
                RefreshCounts();

                if (MultiplayerModeManager.IsLan)
                {
                    SetExpectedPlayerCount(_connectedPlayers.Count == 0 ? 1 : _connectedPlayers.Count);
                }
            }
        }

        private void RegisterPlayerInternal(ulong clientId)
        {
            if (_connectedPlayers.Contains(clientId)) return;

            _connectedPlayers.Add(clientId);
            OnPlayerConnected?.Invoke(clientId);
            RefreshCounts();
        }

        private void RefreshCounts()
        {
            OnPlayerCountChanged?.Invoke(_connectedPlayers.Count);

            bool allPlayersConnectedNow = AreAllPlayersConnected;
            if (allPlayersConnectedNow && !_wasAllPlayersConnected)
            {
                OnAllPlayersConnected?.Invoke();
            }

            _wasAllPlayersConnected = allPlayersConnectedNow;
        }

        public void SetExpectedPlayerCount(int count)
        {
            if (!IsServer) return;

            int sanitized = Mathf.Max(1, count);

            if (_expectedPlayerCount == sanitized) return;

            _expectedPlayerCount = sanitized;
            OnExpectedPlayerCountChanged?.Invoke(_expectedPlayerCount);
            RefreshCounts();
        }

        
        public override void OnNetworkDespawn()
        {
            if (!IsServer || NetworkManager.Singleton == null) return;

            if (_subscribed)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;

                _subscribed = false;
            }
        }
        
        
    }
}