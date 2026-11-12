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
        private const string PLAYER_READY = "PRONTO";
        private const string PLAYER_NOT_READY = "NÃO PRONTO";
        private const string HOST_START = "INICIAR";
        
        private bool _isBusy;

        private void OnEnable()
        {
            Lobby.instance.OnJoinedLobby += LobbyManager_OnPlayerJoinedInLobby;
            Lobby.instance.OnLobbyUpdated += LobbyManager_OnLobbyRefresh;

            // The menu opens this panel only after the lobby is joined, so the join event has already fired.
            RefreshLobbyInfo();
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
            // One request at a time. A second click while the first was still in flight started a
            // second relay session for the host, and flooded the lobby service for the others.
            if (_isBusy) return;
            _isBusy = true;

            MainMenu mainMenu = GetComponentInParent<MainMenu>();
            if (mainMenu != null) mainMenu.SetBusy(true);

            bool started = false;

            try
            {
                if (Lobby.instance.IsHost())
                {
                    _isPlayerReady = true;
                    await Lobby.instance.SetPlayerReady(_isPlayerReady);
                    started = await Lobby.instance.StartGame();
                }
                else
                {
                    _isPlayerReady = !_isPlayerReady;
                    await Lobby.instance.SetPlayerReady(_isPlayerReady);
                    UpdateStartButtonText();
                }
            }
            finally
            {
                _isBusy = false;

                // Started: the match is loading, and the menu stays locked until it is gone.
                if (!started && mainMenu != null) mainMenu.SetBusy(false);
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
            // The slots' volume and mute only write to PlayerPrefs in memory; leaving the lobby,
            // or the match starting, is when they are done moving.
            PlayerPrefs.Save();

            Lobby.instance.OnJoinedLobby -= LobbyManager_OnPlayerJoinedInLobby;
            Lobby.instance.OnLobbyUpdated -= LobbyManager_OnLobbyRefresh;
        }
        
    }

}
