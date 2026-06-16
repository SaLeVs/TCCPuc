using System;
using Network;
using TMPro;
using UnityEngine;


namespace UI
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private TMP_InputField lobbyCodeInputField;
        [SerializeField] private Lobby lobby;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject joinPanel;

        private void Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public async void CreateLobby()
        {
            await lobby.CreateLobbyAsync();
            lobbyPanel.SetActive(true);
        }

        public void JoinLobby()
        {
            string lobbyCode = lobbyCodeInputField.text;
            lobby.JoinLobbyByCode(lobbyCode);
            lobbyPanel.SetActive(true);
            
        }

        public void ToggleLobbyPanel()
        {
            joinPanel.SetActive(!joinPanel.activeSelf);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
        
    }
}

