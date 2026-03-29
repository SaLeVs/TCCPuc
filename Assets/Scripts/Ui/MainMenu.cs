using Network;
using Systems;
using TMPro;
using Unity.Netcode;
using UnityEngine;


namespace UI
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private TMP_InputField lobbyCodeInputField;
        [SerializeField] private Lobby lobby;
        

        public void CreateLobby()
        {
            lobby.CreateLobby();
        }

        public void JoinLobby()
        {
            string lobbyCode = lobbyCodeInputField.text;
            lobby.JoinLobbyByCode(lobbyCode);
            
        }
    }
    
}

