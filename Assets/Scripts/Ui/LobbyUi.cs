using System.Collections.Generic;
using Network;
using TMPro;
using UnityEngine;

namespace UI
{
    public class LobbyUi : MonoBehaviour
    {
        [SerializeField] private List<PlayerLobbySlot> playerSlots;
        
        [SerializeField] private GameObject startGameButton;
        [SerializeField] private GameObject leaveButton;

        [SerializeField] private TextMeshProUGUI startButtonText;
        [SerializeField] private TextMeshProUGUI lobbyCodeText;
        
        
        private bool _isPlayerReady;
        private const string PLAYER_READY = "READY";
        private const string PLAYER_NOT_READY = "NOT READY";
        private const string HOST_START = "START GAME";
        
        private void OnEnable()
        {
            Lobby.instance.OnJoinedLobby += LobbyManager_OnPlayerJoinedInLobby;
            Lobby.instance.OnLobbyUpdated += LobbyManager_OnLobbyRefresh;
        }
        
        
        private void LobbyManager_OnPlayerJoinedInLobby()
        {
            RefreshLobbyInfo();
        }

        private void LobbyManager_OnLobbyRefresh()
        {
            RefreshLobbyInfo();
        }
        
        private void RefreshLobbyInfo()
        {
            Unity.Services.Lobbies.Models.Lobby currentLobby = Lobby.instance.JoinedLobby;
 
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
            
            UpdateStartButtonText();
        }
        
        private void UpdateStartButtonText()
        { 
            startButtonText.text = Lobby.instance.IsHost() ? HOST_START : (_isPlayerReady ? PLAYER_NOT_READY : PLAYER_READY);
        }
        
        public async void StartGameButton()
        { 
            if (Lobby.instance.IsHost())
            {
                _isPlayerReady = true;
                await Lobby.instance.SetPlayerReady(_isPlayerReady);
                await Lobby.instance.StartGame();
            }
            else
            {
                _isPlayerReady = !_isPlayerReady;
                await Lobby.instance.SetPlayerReady(_isPlayerReady);
                UpdateStartButtonText();
            }
        }

        public void LeaveButton()
        {
            Lobby.instance.LeaveLobby();
            gameObject.SetActive(false);
            lobbyCodeText.text = string.Empty;
        }
        
        
        private void OnDisable()
        {
            Lobby.instance.OnJoinedLobby -= LobbyManager_OnPlayerJoinedInLobby;
            Lobby.instance.OnLobbyUpdated -= LobbyManager_OnLobbyRefresh;
        }
        
    }

}
