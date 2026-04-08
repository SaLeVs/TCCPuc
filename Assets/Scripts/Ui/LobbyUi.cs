using System.Collections.Generic;
using Network;
using TMPro;
using UnityEngine;

namespace UI
{
    public class LobbyUi : MonoBehaviour
    {
        [SerializeField] private Lobby lobbyManager;
        
        [SerializeField] private List<PlayerLobbySlot> playerSlots;
        
        [SerializeField] private GameObject startGameButton;
        [SerializeField] private GameObject readyButton;
        [SerializeField] private GameObject leaveButton;

        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private TextMeshProUGUI lobbyCodeText;
        
        
        private bool _isPlayerReady;
        
        
        private void OnEnable()
        {
            lobbyManager.OnJoinedLobby += LobbyManager_OnPlayerJoinedInLobby;
        }

        private void LobbyManager_OnPlayerJoinedInLobby(string lobbyCode)
        {
            Unity.Services.Lobbies.Models.Lobby currentLobby = lobbyManager.JoinedLobby;
 
            if (currentLobby == null) return;
            
            lobbyCodeText.text = currentLobby.LobbyCode;
            
            List<Unity.Services.Lobbies.Models.Player> players = currentLobby.Players;
            string hostId = currentLobby.HostId;
 
            for (int i = 0; i < playerSlots.Count; i++)
            {
                if (i < players.Count)
                {
                    bool isHost = players[i].Id == hostId;
                    playerSlots[i].gameObject.SetActive(true);
                    playerSlots[i].SetPlayer(players[i], isHost);
                }
                else
                {
                    playerSlots[i].gameObject.SetActive(true);
                    playerSlots[i].SetEmpty();
                }
            }

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(lobbyManager.IsHost());
            }
            
        }

        public async void ReadyButton()
        {
            _isPlayerReady = !_isPlayerReady;
            await lobbyManager.SetPlayerReady(_isPlayerReady);
            
            if (readyButton != null)
            {
                readyButtonText.text = _isPlayerReady ? "Not Ready" : "Ready";
            }
            
        }
        
        public async void StartGameButton()
        {
            await lobbyManager.StartGame();
        }

        public void LeaveButton()
        {
            lobbyManager.LeaveLobby();
            gameObject.SetActive(false);
        }
        
        
        private void OnDisable()
        {
            lobbyManager.OnJoinedLobby -= LobbyManager_OnPlayerJoinedInLobby;
        }
        
    }

}
