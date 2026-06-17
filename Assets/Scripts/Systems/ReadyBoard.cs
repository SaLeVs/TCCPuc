using System.Collections.Generic;
using Interfaces;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

namespace Systems
{
    public class ReadyBoard : NetworkBehaviour, IInteractable
    {
        [Header("Ready System")]
        [SerializeField] private PlayersReady playersReady;
        [SerializeField] private Button readyButton;
        [SerializeField] private ReadyIconUI readyIconPrefab;
        [SerializeField] private Transform iconsContainer;

        [Header("Camera")]
        [SerializeField] private CinemachineCamera readyBoardVCam;

        private readonly List<ReadyIconUI> _icons = new();

        private IInputLockable _currentInputLockable;

        private bool _isReady;
        private bool _isInUse;
        
        private int _lastPlayerCount = -1;

        
        public override void OnNetworkSpawn()
        {
            playersReady.OnReadyCountChanged += PlayersReady_OnReadyCountChanged;

            if (PlayerTracker.Instance != null)
            {
                PlayerTracker.Instance.OnPlayerConnected += PlayerTracker_OnPlayerCountChanged;
                PlayerTracker.Instance.OnPlayerDisconnected += PlayerTracker_OnPlayerCountChanged;
            }

            SyncBoard();
        }
        
        private void Update()
        {
            int currentCount = PlayerTracker.Instance.ConnectedPlayerCount;

            if (currentCount != _lastPlayerCount)
            {
                _lastPlayerCount = currentCount;
                SyncBoard();
            }
        }
        

        private void SyncBoard()
        {
            if (PlayerTracker.Instance == null || playersReady == null) return;

            int totalPlayers = PlayerTracker.Instance.ConnectedPlayerCount;
            (int readyCount, _) = playersReady.GetReadyCount();

            if (_icons.Count != totalPlayers)
            {
                RebuildIcons(totalPlayers);
            }

            UpdateIcons(readyCount);
        }

        private void RebuildIcons(int totalPlayers)
        {
            foreach (ReadyIconUI icon in _icons)
            {
                if (icon != null)
                {
                    Destroy(icon.gameObject);
                }
            }

            _icons.Clear();

            for (int i = 0; i < totalPlayers; i++)
            {
                ReadyIconUI icon = Instantiate(readyIconPrefab, iconsContainer);
                icon.SetReady(false);
                _icons.Add(icon);
            }
        }

        private void UpdateIcons(int readyCount)
        {
            int clampedReadyCount = Mathf.Clamp(readyCount, 0, _icons.Count);

            for (int i = 0; i < _icons.Count; i++)
            {
                _icons[i].SetReady(i < clampedReadyCount);
            }
        }

        private void PlayersReady_OnReadyCountChanged(int readyCount, int totalCount)
        {
            if (_icons.Count != totalCount)
            {
                RebuildIcons(totalCount);
            }

            UpdateIcons(readyCount);
        }

        private void PlayerTracker_OnPlayerCountChanged(ulong clientId)
        {
            SyncBoard();
        }

        public bool CanInteract(GameObject interactor)
        {
            return !_isInUse;
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out NetworkObject netObj)) return false;
            if (!netObj.IsOwner) return false;
            if (!playerInteractor.TryGetComponent(out IInputLockable lockable)) return false;

            _isInUse = true;
            _currentInputLockable = lockable;

            _currentInputLockable.SetInputLocked(true);
            readyBoardVCam.Priority = 20;

            return true;
        }

        public void Exit()
        {
            _isInUse = false;

            _currentInputLockable?.SetInputLocked(false);
            _currentInputLockable = null;

            readyBoardVCam.Priority = 0;
        }

        public void SetPlayerReady()
        {
            _isReady = !_isReady;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            if (_isReady)
            {
                playersReady.SetPlayerReadyServerRpc(localClientId);
            }
            else
            {
                playersReady.SetPlayerNotReadyServerRpc(localClientId);
            }
        }

        
        public override void OnNetworkDespawn()
        {
            if (playersReady != null)
            {
                playersReady.OnReadyCountChanged -= PlayersReady_OnReadyCountChanged;
            }

            if (PlayerTracker.Instance != null)
            {
                PlayerTracker.Instance.OnPlayerConnected -= PlayerTracker_OnPlayerCountChanged;
                PlayerTracker.Instance.OnPlayerDisconnected -= PlayerTracker_OnPlayerCountChanged;
            }
        }
        
    }
}