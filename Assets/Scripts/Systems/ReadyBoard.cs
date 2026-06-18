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
            playersReady.OnReadyCountChanged += PlayersReady_OnReadyCountChanged;

            (int readyCount, int totalCount) = playersReady.GetReadyCount();
            RebuildIcons(totalCount);
            UpdateIcons(readyCount);
        }

        private void PlayersReady_OnReadyCountChanged(int readyCount, int totalCount)
        {
            if (_icons.Count != totalCount)
            {
                RebuildIcons(totalCount);
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

            if (_isReady)
            {
                playersReady.SetPlayerReadyRpc();
            }
            else
            {
                playersReady.SetPlayerNotReadyRpc();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (playersReady != null)
            {
                playersReady.OnReadyCountChanged -= PlayersReady_OnReadyCountChanged;
            }
        }
    }
}