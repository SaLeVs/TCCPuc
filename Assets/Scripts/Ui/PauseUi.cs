using System;
using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ui
{
    public class PauseMenuUi : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject optionPanel;
        
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private PlayerCamera playerCamera;

        private bool _isReturningToMenu;
        private bool isPanelEnabled;
        

        private void Start()
        {
            pausePanel.SetActive(false);
            playerCamera.OnPauseToggled += PlayerCamera_OnPauseToggled;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }


        private void PlayerCamera_OnPauseToggled(bool isPaused)
        {
            pausePanel.SetActive(isPaused);
            isPanelEnabled = isPaused;

            Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isPaused;
        }

        public void ClosePauseMenu()
        {
            playerCamera.SetPauseState(false);
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


        public void OpenOptionPanel()
        {
            optionPanel.SetActive(true);
        }
        
        private void OnDestroy()
        {
            playerCamera.OnPauseToggled -= PlayerCamera_OnPauseToggled;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
        
    }
}