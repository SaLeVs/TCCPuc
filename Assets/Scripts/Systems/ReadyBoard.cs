using Interfaces;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

namespace Systems
{
    public class ReadyBoard : NetworkBehaviour, IInteractable
    {
        [Header("Ready System")]
        [SerializeField] private PlayersReady playersReady;
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private TextMeshProUGUI playersReadyCount;

        [Header("Camera")]
        [SerializeField] private CinemachineCamera readyBoardVCam;

        private IInputLockable _currentInputLockable;
        private bool _isReady;
        private bool _isInUse;

        
        private void Awake()
        {
            UpdateButtonVisual();
        }

        
        public override void OnNetworkSpawn()
        {
            playersReady.OnReadyCountChanged += PlayersReady_UpdateCountText;
            PlayerTracker.Instance.OnPlayerConnected += PlayerTracker_OnPlayerConnected;
            PlayerTracker.Instance.OnPlayerDisconnected += PlayerTracker_OnPlayerDisconnected;

            RefreshCount();
        }
        
        
        private void PlayersReady_UpdateCountText(int readyCount, int totalCount)
        {
            playersReadyCount.text = readyCount + " / " + totalCount;
        }
        
        private void PlayerTracker_OnPlayerConnected(ulong clientId) => RefreshCount();
        private void PlayerTracker_OnPlayerDisconnected(ulong clientId) => RefreshCount();
        
        private void RefreshCount()
        {
            (int ready, int total) = playersReady.GetReadyCount();
            PlayersReady_UpdateCountText(ready, total);
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
                playersReady.SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                playersReady.SetPlayerNotReadyServerRpc(NetworkManager.Singleton.LocalClientId);
            }

            UpdateButtonVisual();
        }

        private void UpdateButtonVisual()
        {
            buttonText.text = _isReady ? "Ready" : "Not Ready";
        }

        
        public override void OnNetworkDespawn()
        {
            playersReady.OnReadyCountChanged -= PlayersReady_UpdateCountText;

            if (PlayerTracker.Instance != null)
            {
                PlayerTracker.Instance.OnPlayerConnected -= PlayerTracker_OnPlayerConnected;
                PlayerTracker.Instance.OnPlayerDisconnected -= PlayerTracker_OnPlayerDisconnected;
            }
        }
        
        
    }
}