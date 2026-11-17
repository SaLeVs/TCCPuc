using Network;
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

        [Header("Voz")]
        [Tooltip("Linha com o mudo e o volume. Some no slot do próprio jogador e nos vazios.")]
        [SerializeField] private GameObject voiceControls;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle muteToggle;

        private Unity.Services.Lobbies.Models.Player _player;
        private const string HOST_TAG = "ANFITRIÃO";
        private const string USER_DATA_KEY = "UserData";
        private const string READY_KEY = "Ready";

        private const string PLAYER_INFO_PLACEHOLDER =
            "Este profissional tem o direito de gravar cenas de baixa qualidade, " +
            "usar materiais duvidosos e tentar explorar o teatro em benefício da audiência e do regulamento.";

        private const string PLAYER_NULL_INFO = "AGUARDANDO...";

        // Same scale as the pause menu's sliders: Vivox's -50..50, 0 unchanged.
        private const int MIN_VOLUME = -50;
        private const int MAX_VOLUME = 50;

        // Whose voice the controls are showing. The lobby refreshes every few seconds; the saved
        // values are read only when this changes, so a slider being dragged is never yanked back.
        private string _voicePlayerId;
        private VivoxManager _vivox;
        private bool _isInitialized;


        // Not Awake: LobbyUi fills the slots from its own OnEnable, which can run before the slots'
        // Awake when the panel opens.
        private void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (volumeSlider != null)
            {
                volumeSlider.minValue = MIN_VOLUME;
                volumeSlider.maxValue = MAX_VOLUME;
                volumeSlider.wholeNumbers = true;
                volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            }

            if (muteToggle != null) muteToggle.onValueChanged.AddListener(HandleMuteChanged);
        }

        public void SetPlayer(Unity.Services.Lobbies.Models.Player player, bool isHost = false)
        {
            Initialize();
            _player = player;
            UserData userData = ParseUserData(player);

            SetPlayerName(userData);
            SetPlayerInfo(userData, isHost);
            SetReadyStatus(player, userData);
            SetIconImage(userData);

            // Nothing to adjust on this machine's own slot.
            SetVoiceControls(Network.Lobby.IsLocalPlayer(player.Id) ? null : player.Id);
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
            Initialize();
            playerNameText.text = PLAYER_NULL_INFO;

            if (playerInfoText != null)
                playerInfoText.text = "";

            if (playerHost != null)
                playerHost.text = "";

            if (playerImage != null)
                playerImage.sprite = defaultAvatar;

            readyText.text = "";

            SetVoiceControls(null);
        }

        /// <summary>
        /// Shows the volume and mute for <paramref name="playerId"/> — the lobby player id, which is
        /// the same Unity Authentication id Vivox keys every participant by — or hides them on null.
        /// </summary>
        private void SetVoiceControls(string playerId)
        {
            if (voiceControls != null) voiceControls.SetActive(!string.IsNullOrEmpty(playerId));

            if (string.IsNullOrEmpty(playerId) || playerId == _voicePlayerId)
            {
                if (string.IsNullOrEmpty(playerId)) _voicePlayerId = null;
                return;
            }

            _voicePlayerId = playerId;

            VivoxManager vivox = GetVivox();
            bool isMuted = vivox != null && vivox.IsParticipantLocallyMuted(playerId);

            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(isMuted);

            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(vivox != null ? vivox.GetParticipantVolume(playerId) : 0);
                volumeSlider.interactable = !isMuted;
            }
        }

        private void HandleVolumeChanged(float value)
        {
            VivoxManager vivox = GetVivox();
            if (vivox != null) vivox.SetParticipantVolume(_voicePlayerId, Mathf.RoundToInt(value));
        }

        private void HandleMuteChanged(bool isMuted)
        {
            if (volumeSlider != null) volumeSlider.interactable = !isMuted;

            VivoxManager vivox = GetVivox();
            if (vivox != null) vivox.SetParticipantLocalMute(_voicePlayerId, isMuted);
        }

        // Looked up quietly: VivoxManager.instance logs an error every time it is missing, and a LAN
        // lobby has no voice at all.
        private VivoxManager GetVivox()
        {
            if (_vivox == null) _vivox = FindFirstObjectByType<VivoxManager>();
            return _vivox;
        }

        public override void OnDestroy()
        {
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(HandleVolumeChanged);
            if (muteToggle != null) muteToggle.onValueChanged.RemoveListener(HandleMuteChanged);

            base.OnDestroy();
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
