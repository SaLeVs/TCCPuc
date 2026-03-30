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
    }
    
}

