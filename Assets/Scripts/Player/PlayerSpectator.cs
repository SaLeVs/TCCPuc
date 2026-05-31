using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerSpectator : NetworkBehaviour
    {
        public event Action<string> OnTargetChanged;

        [SerializeField] private PlayerState playerState;
        [SerializeField] private GameObject gameplayCanvas;
        [SerializeField] private GameObject spectatorCanvas;

        private readonly List<ulong> _alivePlayerIds = new();
        private int _currentIndex;
        private CinemachineCamera _currentTargetVCam;

        private const int SPECTATOR_PRIORITY = 20;

        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            
            playerState.OnPlayerDead += PlayerState_OnDeathChanged;
        }

        
        private void PlayerState_OnDeathChanged(bool isDead)
        {
            if (!isDead) return;

            gameplayCanvas.SetActive(false);
            spectatorCanvas.SetActive(true);

            playerState.SetSpectatorMode(true);

            RefreshAliveList();

            if (_alivePlayerIds.Count > 0)
            {
                SetTarget(0);
            }
            
        }

        private void RefreshAliveList()
        {
            foreach (ulong clientId in _alivePlayerIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient) 
                    && networkClient.PlayerObject != null 
                    && networkClient.PlayerObject.TryGetComponent(out PlayerState playerStateClient))
                {
                    playerStateClient.OnPlayerDead -= PlayerState_OnWatchedPlayerDied;
                }
            }

            _alivePlayerIds.Clear();

            foreach (KeyValuePair<ulong, NetworkClient> clientsInGame in NetworkManager.Singleton.ConnectedClients)
            {
                if (clientsInGame.Key == OwnerClientId) continue;

                NetworkObject playerObj = clientsInGame.Value.PlayerObject;

                if (playerObj != null && playerObj.TryGetComponent(out PlayerState playerStateClient) && !playerStateClient.IsDead)
                {
                    _alivePlayerIds.Add(clientsInGame.Key);
                    playerStateClient.OnPlayerDead += PlayerState_OnWatchedPlayerDied;
                }
            }
        }

        private void PlayerState_OnWatchedPlayerDied(bool isDead)
        {
            if (!isDead) return;
            
            RefreshAliveList();
            
            if (_alivePlayerIds.Count > 0)
            {
                SetTarget(0);
            }
        }

        public void NextPlayer()
        {
            if (_alivePlayerIds.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % _alivePlayerIds.Count;
            SetTarget(_currentIndex);
        }

        public void PreviousPlayer()
        {
            if (_alivePlayerIds.Count == 0) return;
            _currentIndex = (_currentIndex - 1 + _alivePlayerIds.Count) % _alivePlayerIds.Count;
            SetTarget(_currentIndex);
        }

        private void SetTarget(int index)
        {
            _currentIndex = index;

            if (_currentTargetVCam != null)
            {
                _currentTargetVCam.Priority = 0;
            }

            ulong targetId = _alivePlayerIds[index];

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient client) || client.PlayerObject == null) return;

            if (client.PlayerObject.TryGetComponent(out PlayerState targetState))
            {
                _currentTargetVCam = targetState.PlayerCinemachineCamera;
                _currentTargetVCam.Priority = SPECTATOR_PRIORITY;
            }

            OnTargetChanged?.Invoke($"Player {targetId}");
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            if (_currentTargetVCam != null)
            {
                _currentTargetVCam.Priority = 0;
            }

            playerState.OnPlayerDead -= PlayerState_OnDeathChanged;
        }
    }
}