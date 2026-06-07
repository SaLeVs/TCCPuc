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

        public override void OnNetworkSpawn()
        {
            playersReady.OnReadyCountChanged += PlayersReady_OnUpdateIcons;
            PlayerTracker.Instance.OnPlayerConnected += PlayerTracker_OnPlayerCountChanged;
            PlayerTracker.Instance.OnPlayerDisconnected += PlayerTracker_OnPlayerCountChanged;

            SpawnIcons();
            RefreshCount();
        }

        private void SpawnIcons()
        {
            foreach (var icon in _icons) Destroy(icon.gameObject);
            _icons.Clear();

            int total = PlayerTracker.Instance.ConnectedPlayerCount;

            for (int i = 0; i < total; i++)
            {
                var icon = Instantiate(readyIconPrefab, iconsContainer);
                icon.SetReady(false);
                _icons.Add(icon);
            }
        }

        private void PlayersReady_OnUpdateIcons(int readyCount, int totalCount)
        {
            for (int i = 0; i < _icons.Count; i++)
            {
                _icons[i].SetReady(i < readyCount);
            }
        }

        private void PlayerTracker_OnPlayerCountChanged(ulong clientId)
        {
            SpawnIcons();
            RefreshCount();
        }

        private void RefreshCount()
        {
            (int ready, int total) = playersReady.GetReadyCount();
            PlayersReady_OnUpdateIcons(ready, total);
        }

        public bool CanInteract(GameObject interactor) => !_isInUse;

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

            if (_isReady)
            {
                playersReady.SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                playersReady.SetPlayerNotReadyServerRpc(NetworkManager.Singleton.LocalClientId);
            }
        }

        
        public override void OnNetworkDespawn()
        {
            playersReady.OnReadyCountChanged -= PlayersReady_OnUpdateIcons; 

            if (PlayerTracker.Instance != null)
            {
                PlayerTracker.Instance.OnPlayerConnected -= PlayerTracker_OnPlayerCountChanged;
                PlayerTracker.Instance.OnPlayerDisconnected -= PlayerTracker_OnPlayerCountChanged;
            }
            
        }
        
        
    }
}