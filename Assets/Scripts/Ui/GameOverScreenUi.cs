using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ui
{
    public class GameOverScreenUi : MonoBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool _isReturningToMenu;

        private void Start()
        {
            playerState.OnGameOverTriggered += PlayerState_OnGameOverTriggered;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void PlayerState_OnGameOverTriggered()
        {
            gameOverPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
            playerState.OnGameOverTriggered -= PlayerState_OnGameOverTriggered;
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }
}