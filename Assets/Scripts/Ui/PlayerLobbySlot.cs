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
        [SerializeField] private TMP_Text readyText;
        [SerializeField] private TMP_Text playerHost;
        [SerializeField] private Sprite defaultAvatar;
        
        [SerializeField] private int avatarSlotIndex = 0;
        [SerializeField] private Sprite[] playerAvatar;
        

        
        private Unity.Services.Lobbies.Models.Player _player;
        private const string HOST_TAG = "HOST";
        
        private const string PLAYER_INFO_PLACEHOLDER = 
            "This professional has the right to record poor-quality scenes, " +
            "use questionable materials, and attempt to exploit the theater for the benefit of the audience and its regulation.";
        
        private const string PLAYER_NULL_INFO = "WAITING...";
        
        
        public void SetPlayer(Unity.Services.Lobbies.Models.Player player, bool isHost = false)
        {
            _player = player;
 
            SetPlayerName(player);
            SetPlayerInfo(player, isHost);
            SetReadyStatus(player);
            SetIconImage(player);
        }
        
        private void SetPlayerName(Unity.Services.Lobbies.Models.Player player)
        {
            string name = GetPlayerData(player, "PlayerName", $"Player {player.Id[..4]}");
            playerNameText.text = name;
        }
 
        private void SetPlayerInfo(Unity.Services.Lobbies.Models.Player player, bool isHost)
        {
            if (playerInfoText == null) return;
            
            playerInfoText.text = PLAYER_INFO_PLACEHOLDER;
            playerHost.text = isHost ? HOST_TAG : "CLIENT";
            
        }
 
        private void SetReadyStatus(Unity.Services.Lobbies.Models.Player player)
        {
            bool isReady = GetPlayerData(player, "Ready", "0") == "1";
            
            string playerName = GetPlayerData(player, "PlayerName", $"Player {player.Id[..4]}");
            readyText.text = isReady ? $"{playerName}" : "";
        }

        private void SetIconImage(Unity.Services.Lobbies.Models.Player player)
        {
            int index = Mathf.Clamp(avatarSlotIndex, 0, playerAvatar.Length - 1);
            playerImage.sprite = playerAvatar[index];
        }
        
        public void SetEmpty()
        {
            playerNameText.text = PLAYER_NULL_INFO;
 
            if (playerInfoText != null)
                playerInfoText.text = "";
 
            if (playerImage != null)
                playerImage.sprite = defaultAvatar;
            
            readyText.text = "";
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

