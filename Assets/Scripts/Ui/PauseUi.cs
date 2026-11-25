using System;
using System.Threading.Tasks;
using Network;
using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            // The options (and the device panel inside them) are saved switched on in the prefab, so
            // the very first pause opened straight onto "Dispositivos". Their Awake has run by now -
            // it is what applies the saved volumes - so they can be put away.
            CloseOptionPanel();
            pausePanel.SetActive(false);
            playerCamera.OnPauseToggled += PlayerCamera_OnPauseToggled;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }


        private void PlayerCamera_OnPauseToggled(bool isPaused)
        {
            // Options lives inside the pause canvas, so whatever was left open there came back with
            // it. Closed both ways, every pause starts on the pause menu itself.
            CloseOptionPanel();

            pausePanel.SetActive(isPaused);
            isPanelEnabled = isPaused;

            // The cursor is PlayerCamera.s business: it holds the Paused reason and knows about
            // the other reasons this one never could.
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

        private async void GoToMainMenu()
        {
            _isReturningToMenu = true;

            try
            {
                if (VivoxManager.instance != null)
                {
                    VivoxManager.instance.LeaveVoiceChannel();
                }

                if (Lobby.instance != null)
                {
                    Lobby.instance.LeaveLobby();
                }

                if (NetworkManager.Singleton != null)
                {
                    HostSingleton.instance?.gameManager?.Dispose();
                    ClientSingleton.instance?.gameManager?.Dispose();
                    NetworkManager.Singleton?.Shutdown();
                }

                await Task.Delay(500);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }


        public void OpenOptionPanel()
        {
            optionPanel.SetActive(true);
        }

        private void CloseOptionPanel()
        {
            if (optionPanel.TryGetComponent(out OptionsMenuManager optionsMenu))
            {
                optionsMenu.CloseSubPanels();
            }

            optionPanel.SetActive(false);
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