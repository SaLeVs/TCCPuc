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

        [SerializeField] private Sprite[] playerAvatar;

        private Unity.Services.Lobbies.Models.Player _player;
        private const string HOST_TAG = "ANFITRIÃO";
        private const string USER_DATA_KEY = "UserData";
        private const string READY_KEY = "Ready";

        private const string PLAYER_INFO_PLACEHOLDER =
            "Este profissional tem o direito de gravar cenas de baixa qualidade, " +
            "usar materiais duvidosos e tentar explorar o teatro em benefício da audiência e do regulamento.";

        private const string PLAYER_NULL_INFO = "AGUARDANDO...";


        public void SetPlayer(Unity.Services.Lobbies.Models.Player player, bool isHost = false)
        {
            _player = player;
            UserData userData = ParseUserData(player);

            SetPlayerName(userData);
            SetPlayerInfo(userData, isHost);
            SetReadyStatus(player, userData);
            SetIconImage(userData);
        }

        private UserData ParseUserData(Unity.Services.Lobbies.Models.Player player)
        {
            string fallbackName = $"Jogador {player.Id[..4]}";

            if (player.Data != null && player.Data.TryGetValue(USER_DATA_KEY, out PlayerDataObject data))
            {
                UserData userData = JsonUtility.FromJson<UserData>(data.Value);
                
                if (userData != null && !string.IsNullOrWhiteSpace(userData.playerName))
                {
                    return userData;
                }
            }

            return new UserData { playerName = fallbackName };
        }

        private void SetPlayerName(UserData userData)
        {
            playerNameText.text = userData.playerName;
        }

        private void SetPlayerInfo(UserData userData, bool isHost)
        {
            if (playerInfoText == null) return;
            
            playerInfoText.text = string.IsNullOrEmpty(userData.description) ? PLAYER_INFO_PLACEHOLDER : userData.description;
            playerHost.text = isHost ? HOST_TAG : "CONVIDADO";
        }

        private void SetReadyStatus(Unity.Services.Lobbies.Models.Player player, UserData userData)
        {
            bool isReady = GetPlayerData(player, READY_KEY, "0") == "1";
            readyText.text = isReady ? userData.playerName : "";
        }

        private void SetIconImage(UserData userData)
        {
            // Change for steam avatar later
            int index = Mathf.Clamp(userData.avatarIndex, 0, playerAvatar.Length - 1);
            playerImage.sprite = playerAvatar.Length > 0 ? playerAvatar[index] : defaultAvatar;
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