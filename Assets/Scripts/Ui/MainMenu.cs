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
        [SerializeField] private GameObject onlinePanel;
        [SerializeField] private GameObject lanPanel;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject optionPanel;

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

        public void OpenOnlinePanel()
        {
            lanPanel.SetActive(false);
            onlinePanel.SetActive(true);
        }

        public void OpenLanPanel()
        {
            onlinePanel.SetActive(false);
            lanPanel.SetActive(true);
        }

        public void ReturnToMenuPanel()
        {
            lanPanel.SetActive(false);
            onlinePanel.SetActive(false);
            menuPanel.SetActive(true);
        }

        public void OpenOptionPanel()
        {
            optionPanel.SetActive(true);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
        
    }
}

