using System;
using System.Collections;
using System.Collections.Generic;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionTransferManager : NetworkBehaviour
    {
        [SerializeField] private MissionManager missionManager;
        
        private readonly Dictionary<ulong, (PlayerDead dead, Action<bool> handler)> _registrations = new();
        private const float TIMEOUT = 60f;
        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                StartCoroutine(RegisterWhenReady(clientId));
            }
        }

        
        private void OnClientConnected(ulong clientId) => StartCoroutine(RegisterWhenReady(clientId));

        private IEnumerator RegisterWhenReady(ulong clientId)
        {
            float elapsed = 0f;

            while (elapsed < TIMEOUT)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null 
                    && client.PlayerObject.TryGetComponent(out PlayerDead playerDead))
                {
                    Action<bool> handler = isDead =>
                    {
                        if (isDead)
                        {
                            missionManager.HandlePlayerDeath(clientId);
                        }
                    };

                    playerDead.OnDeathEvent += handler;
                    _registrations[clientId] = (playerDead, handler);
                    Debug.Log($"MissionTransferManager: Watching player {clientId} for death.");
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogWarning($"MissionTransferManager: Timed out waiting for PlayerObject of client {clientId}.");
        }

        private void OnClientDisconnected(ulong clientId) => UnregisterClient(clientId);

        private void UnregisterClient(ulong clientId)
        {
            if (!_registrations.TryGetValue(clientId, out var playerRegister)) return;

            if (playerRegister.dead != null)
            {
                playerRegister.dead.OnDeathEvent -= playerRegister.handler;
            }

            _registrations.Remove(clientId);
        }

        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            foreach (ulong clientId in new List<ulong>(_registrations.Keys))
            {
                UnregisterClient(clientId);
            }

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
    }
}

