using System;
using System.Collections;
using Enums;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerSpectator : NetworkBehaviour
    {
        /// <summary>
        /// The name of the player being watched, raised again whenever that name changes (it may
        /// replicate after the target was picked, arriving empty first). Null when nobody is being
        /// watched anymore.
        /// </summary>
        public event Action<string> OnTargetChanged;

        [SerializeField] private PlayerState playerState;
        [SerializeField] private GameObject gameplayCanvas;
        [SerializeField] private GameObject spectatorCanvas;
        [SerializeField] private float timeToEnterInSpectator;

        private readonly List<ulong> _alivePlayerIds = new();
        private int _currentIndex;
        private CinemachineCamera _currentTargetVCam;

        private const int SPECTATOR_PRIORITY = 20;
        private Coroutine _spectatorCoroutine;
        private PlayerState _currentTargetState;
        private PlayerInfos _watchedInfos;

        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                gameplayCanvas.SetActive(false);
                spectatorCanvas.SetActive(false);
                return;
            }

            gameplayCanvas.SetActive(true);
            spectatorCanvas.SetActive(false);

            playerState.OnPlayerDead += PlayerState_OnDeathChanged;
            playerState.OnPlayerWon += PlayerState_OnPlayerWon;
        }

        
        private void PlayerState_OnDeathChanged(bool isDead)
        {
            if (!isDead) return;
            
            if (_spectatorCoroutine != null)
            {
                StopCoroutine(_spectatorCoroutine);
            }
            
            _spectatorCoroutine = StartCoroutine(StartSpectatorMode());
        }
        
        private IEnumerator StartSpectatorMode()
        {
            yield return new WaitForSeconds(timeToEnterInSpectator);
            
            gameplayCanvas.SetActive(false);
            spectatorCanvas.SetActive(true);

            CursorState.Hold(CursorReason.Spectating);

            WatchFirstAliveOrOwnView();

            _spectatorCoroutine = null;
        }


        private void PlayerState_OnPlayerWon()
        {
            gameplayCanvas.SetActive(false);
            spectatorCanvas.SetActive(true);

            CursorState.Hold(CursorReason.Spectating);

            WatchFirstAliveOrOwnView();
        }

        /// <summary>
        /// Hands the view to someone still playing, or keeps this player's own parked view when
        /// there is nobody.
        ///
        /// <para>It used to drop this camera's priority first and only then look for a target.
        /// With nobody left alive to watch, that left no camera in charge, and Cinemachine fell back
        /// to whatever other camera happened to be live.</para>
        /// </summary>
        private void WatchFirstAliveOrOwnView()
        {
            RefreshAliveList();

            if (_alivePlayerIds.Count > 0)
            {
                playerState.SetSpectatorMode(true);
                SetTarget(0);
                return;
            }

            // Someone who escaped has no view of their own worth going back to; they stay as the
            // win left them.
            if (playerState.HasWon)
            {
                playerState.SetSpectatorMode(true);
                ShowTargetName(null);
                return;
            }

            ReleaseCurrentTarget();
            playerState.SetSpectatorMode(false);
        }

        private void ReleaseCurrentTarget()
        {
            if (_currentTargetVCam != null)
            {
                _currentTargetVCam.Priority = 0;
                _currentTargetState?.SetOcclusionVisible(true);
            }

            _currentTargetVCam = null;
            _currentTargetState = null;
            ShowTargetName(null);
        }

        /// <summary>
        /// Follows the watched player's name, so the label fills in once it replicates.
        /// </summary>
        private void ShowTargetName(PlayerInfos infos)
        {
            if (_watchedInfos != null)
            {
                _watchedInfos.PlayerName.OnValueChanged -= WatchedPlayer_OnNameChanged;
            }

            _watchedInfos = infos;

            if (_watchedInfos == null)
            {
                OnTargetChanged?.Invoke(null);
                return;
            }

            _watchedInfos.PlayerName.OnValueChanged += WatchedPlayer_OnNameChanged;
            OnTargetChanged?.Invoke(_watchedInfos.PlayerName.Value.ToString());
        }

        private void WatchedPlayer_OnNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
        {
            OnTargetChanged?.Invoke(newName.ToString());
        }

        private void RefreshAliveList()
        {
            foreach (ulong clientId in _alivePlayerIds)
            {
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient networkClient) && networkClient.PlayerObject != null && networkClient.PlayerObject.TryGetComponent(out PlayerState playerStateClient))
                {
                    playerStateClient.OnPlayerDead -= PlayerState_OnWatchedPlayerDied;
                    playerStateClient.OnPlayerWon -= PlayerState_OnWatchedPlayerWon;
                }
            }

            _alivePlayerIds.Clear();

            foreach (KeyValuePair<ulong, NetworkClient> clientsInGame in NetworkManager.Singleton.ConnectedClients)
            {
                if (clientsInGame.Key == OwnerClientId) continue;

                NetworkObject playerObj = clientsInGame.Value.PlayerObject;

                if (playerObj != null && playerObj.TryGetComponent(out PlayerState playerStateClient) && !playerStateClient.IsDead && !playerStateClient.HasWon)
                {
                    _alivePlayerIds.Add(clientsInGame.Key);
                    playerStateClient.OnPlayerDead += PlayerState_OnWatchedPlayerDied;
                    playerStateClient.OnPlayerWon += PlayerState_OnWatchedPlayerWon;
                }
            }
        }

        private void PlayerState_OnWatchedPlayerWon()
        {
            WatchFirstAliveOrOwnView();
        }

        private void PlayerState_OnWatchedPlayerDied(bool isDead)
        {
            if (!isDead) return;

            WatchFirstAliveOrOwnView();
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
                _currentTargetState?.SetOcclusionVisible(true);
            }

            ulong targetId = _alivePlayerIds[index];

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                ShowTargetName(null);
                return;
            }

            if (client.PlayerObject.TryGetComponent(out PlayerState targetState))
            {
                _currentTargetVCam = targetState.PlayerCinemachineCamera;
                _currentTargetVCam.Priority = SPECTATOR_PRIORITY;

                _currentTargetState = targetState;
                _currentTargetState.SetOcclusionVisible(false);
            }

            ShowTargetName(targetState != null ? targetState.PlayerInfos : null);
        }

        
        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            if (_currentTargetVCam != null)
            {
                _currentTargetVCam.Priority = 0;
            }
            
            _currentTargetState?.SetOcclusionVisible(true);

            if (_watchedInfos != null)
            {
                _watchedInfos.PlayerName.OnValueChanged -= WatchedPlayer_OnNameChanged;
                _watchedInfos = null;
            }

            playerState.OnPlayerDead -= PlayerState_OnDeathChanged;
            playerState.OnPlayerWon -= PlayerState_OnPlayerWon;
        }
        
    }
}