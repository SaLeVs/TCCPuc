using System;
using Enums;
using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ui
{
    public class VictoryScreenUi : MonoBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool _isReturningToMenu;

        private void Start()
        {
            playerState.OnVictoryTriggered += PlayerState_OnVictoryTriggered;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void PlayerState_OnVictoryTriggered()
        {
            victoryPanel.SetActive(true);
            CursorState.Hold(CursorReason.Victory);
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (_isReturningToMenu) return;
            if (NetworkManager.Singleton.IsHost) return;

            GoToMainMenu();
        }
        
        public void ReturnToMainMenu()
        {
            if (_isReturningToMenu) return;
            GoToMainMenu();
        }

        private void GoToMainMenu()
        {
            _isReturningToMenu = true;
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void OnDestroy()
        {
            playerState.OnVictoryTriggered -= PlayerState_OnVictoryTriggered;

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        
    }
}

