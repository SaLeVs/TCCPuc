using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

namespace UI
{
    public class PlayerLobbySlot : NetworkBehaviour
    {
        [SerializeField] private Image playerImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text playerInfoText;
        [SerializeField] private GameObject playerReadyCheck;
        [SerializeField] private Image readyIndicator;
        
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;
        
        [SerializeField] private Sprite defaultAvatar;
        [SerializeField] private Sprite playerAvatar;

        
        private Unity.Services.Lobbies.Models.Player _player;
        private const string HOST_TAG = "[HOST]";
        private const string PLAYER_INFO_PLACEHOLDER = "All infos about player, just placeholder for test font";
        private const string PLAYER_NULL_INFO = "WAITING...";
        
        
        public void SetPlayer(Unity.Services.Lobbies.Models.Player player, bool isHost = false)
        {
            _player = player;
 
            SetPlayerName(player, isHost);
            SetPlayerInfo(player, isHost);
            SetReadyStatus(player);
            SetIconImage(player);
        }
        
        private void SetPlayerName(Unity.Services.Lobbies.Models.Player player, bool isHost)
        {
            string name = GetPlayerData(player, "PlayerName", $"Player {player.Id[..4]}");
            string hostTag = isHost ? HOST_TAG : null;
            playerNameText.text = name + hostTag;
        }
 
        private void SetPlayerInfo(Unity.Services.Lobbies.Models.Player player, bool isHost)
        {
            if (playerInfoText == null) return;
            
            playerInfoText.text = PLAYER_INFO_PLACEHOLDER;
        }
 
        private void SetReadyStatus(Unity.Services.Lobbies.Models.Player player)
        {
            bool isReady = GetPlayerData(player, "Ready", "0") == "1";
 
            if (playerReadyCheck != null)
                playerReadyCheck.SetActive(isReady);
 
            if (readyIndicator != null)
                readyIndicator.color = isReady ? readyColor : notReadyColor;
        }

        private void SetIconImage(Unity.Services.Lobbies.Models.Player player)
        {
            playerImage.sprite = playerAvatar;
        }
        
        public void SetEmpty()
        {
            playerNameText.text = PLAYER_NULL_INFO;
 
            if (playerInfoText != null)
                playerInfoText.text = "";
 
            if (playerImage != null)
                playerImage.sprite = defaultAvatar;
 
            if (playerReadyCheck != null)
                playerReadyCheck.SetActive(false);
 
            if (readyIndicator != null)
                readyIndicator.color = notReadyColor;
        }
 
        private static string GetPlayerData(Unity.Services.Lobbies.Models.Player player, string key, string fallback)
        {
            if (player.Data != null && player.Data.TryGetValue(key, out PlayerDataObject data))
            {
                return data.Value;
            }
            
            return fallback;
        }
        
    }
}

