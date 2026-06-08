using System;
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

        
        private void Start()
        {
            playerState.OnVictoryTriggered += PlayerState_OnVictoryTriggered;
        }

        
        private void PlayerState_OnVictoryTriggered()
        {
            victoryPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ReturnToMainMenu()
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        
        private void OnDestroy()
        {
            playerState.OnVictoryTriggered -= PlayerState_OnVictoryTriggered;
        }
        
        
    }
}

