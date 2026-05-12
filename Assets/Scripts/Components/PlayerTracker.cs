using System;
using System.Collections.Generic;
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
        public event Action OnAllPlayersConnected;
        
        public bool AreAllPlayersConnected => expectedPlayerCount > 0 && _connectedPlayers.Count >= expectedPlayerCount;
        public IReadOnlyList<ulong> ConnectedPlayers => _connectedPlayers;
        public int ConnectedPlayerCount => _connectedPlayers.Count;
        
        private readonly List<ulong> _connectedPlayers = new List<ulong>();
        private int expectedPlayerCount;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                RegisterPlayer(clientId);
            }
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            _connectedPlayers.Clear();
            
            if (expectedPlayerCount == 0) return;

            Debug.Log($"PlayerTracker: Scene {sceneName} loaded — resetting players");

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                RegisterPlayer(clientId);
            }
        }

        private void OnClientConnected(ulong clientId) => RegisterPlayer(clientId);

        private void OnClientDisconnected(ulong clientId)
        {
            _connectedPlayers.Remove(clientId);
            OnPlayerDisconnected?.Invoke(clientId);
            Debug.Log($"PlayerTracker: Player disconnected: {clientId} — total: {_connectedPlayers.Count}");
        }

        private void RegisterPlayer(ulong clientId)
        {
            if (_connectedPlayers.Contains(clientId)) return;

            _connectedPlayers.Add(clientId);
            OnPlayerConnected?.Invoke(clientId);
            Debug.Log($"PlayerTracker: New player connected: {clientId} — total: {_connectedPlayers.Count}");

            if (AreAllPlayersConnected)
            {
                Debug.Log("PlayerTracker: All players connected!");
                OnAllPlayersConnected?.Invoke();
            }
        }
        
        public void SetExpectedPlayerCount(int count)
        {
            expectedPlayerCount = count;
            Debug.Log($"PlayerTracker: Waiting for {count} players.");

            if (AreAllPlayersConnected)
            {
                Debug.Log("PlayerTracker: All players already connected!");
                OnAllPlayersConnected?.Invoke();
            }
        }

        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }
        
    }
}