using System;
using Components;
using Enums;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.Events;


namespace UI
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private TMP_InputField lobbyCodeInputField;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject onlinePanel;
        [SerializeField] private GameObject lanPanel;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject optionPanel;
        [SerializeField] private GameObject creditsPanel; // CRÉDITOS

        [Header("Avisos")]
        [Tooltip("Aviso do painel Online: código errado, partida não encontrada, carregando...")]
        [SerializeField] private MenuNoticePanel onlineNotice;

        [Tooltip("Aviso do painel LAN: porta ou IP inválidos, host que não responde, carregando...")]
        [SerializeField] private MenuNoticePanel lanNotice;

        [Header("Online")]
        [Tooltip("Menor e maior tamanho aceitos para o código da partida (o código do Lobby tem 6).")]
        [SerializeField, Min(1)] private int lobbyCodeMinLength = 6;
        [SerializeField, Min(1)] private int lobbyCodeMaxLength = 6;

        [Tooltip("Roda só depois que a partida foi criada ou encontrada: leva a câmera e abre o painel do lobby.")]
        [SerializeField] private UnityEvent onLobbyEntered;

        [Header("LAN")]
        [SerializeField] private Lan lan;

        private CanvasGroup _lockGroup;
        private Func<string, bool> _presenter;
        private bool _isBusy;

        private void Awake()
        {
            // Locks every button of this canvas at once. A group on the canvas root is combined with
            // the panels' own groups (PanelDissolve), so it never fights their fades.
            _lockGroup = GetComponent<CanvasGroup>();
            if (_lockGroup == null) _lockGroup = gameObject.AddComponent<CanvasGroup>();

            _presenter = PresentConnectionMessage;

            if (onlineNotice != null) onlineNotice.Closed += RefreshLock;
            if (lanNotice != null) lanNotice.Closed += RefreshLock;

            // Enter in the code field does what the ENTRAR mirror next to it does.
            if (lobbyCodeInputField != null) lobbyCodeInputField.onSubmit.AddListener(LobbyCode_OnSubmit);
        }

        private void LobbyCode_OnSubmit(string _) => JoinLobby();

        private void OnEnable()
        {
            ConnectionFeedback.Presenter = _presenter;
        }

        private void OnDisable()
        {
            if (ConnectionFeedback.Presenter == _presenter) ConnectionFeedback.Presenter = null;
        }

        private void Start()
        {
            CursorState.Hold(CursorReason.MainMenu);
        }

        public async void CreateLobby()
        {
            if (_isBusy) return;

            SetBusy(true);
            ShowWaiting(onlineNotice, "Criando a partida...");

            bool created = await Lobby.instance.CreateLobbyAsync();

            SetBusy(false);

            if (!created)
            {
                FailIfStillWaiting(onlineNotice, "Não foi possível criar a partida. Tente de novo.");
                return;
            }

            EnterLobby();
        }

        public async void JoinLobby()
        {
            if (_isBusy) return;

            string lobbyCode = NormalizeLobbyCode(lobbyCodeInputField.text);
            string problem = CheckLobbyCode(lobbyCode);

            // A wrong code never reaches the service and never leaves this panel.
            if (problem != null)
            {
                ShowNotice(onlineNotice, problem);
                return;
            }

            SetBusy(true);
            ShowWaiting(onlineNotice, "Procurando a partida...");

            bool joined = await Lobby.instance.JoinLobbyByCodeAsync(lobbyCode);

            SetBusy(false);

            if (!joined)
            {
                FailIfStillWaiting(onlineNotice, "Não foi possível entrar na partida. Tente de novo.");
                return;
            }

            EnterLobby();
        }

        /// <summary>Only after the lobby exists: the camera move and the lobby panel used to run on the click itself.</summary>
        private void EnterLobby()
        {
            HideNotice(onlineNotice);
            onLobbyEntered?.Invoke();
            lobbyPanel.SetActive(true);
        }

        private static string NormalizeLobbyCode(string text)
        {
            // TMP can leave a zero-width space behind in its fields.
            return (text ?? string.Empty).Replace("​", string.Empty).Trim().ToUpperInvariant();
        }

        private string CheckLobbyCode(string code)
        {
            if (code.Length == 0) return "Digite o código da partida.";

            if (code.Length < lobbyCodeMinLength || code.Length > lobbyCodeMaxLength)
            {
                string expected = lobbyCodeMinLength == lobbyCodeMaxLength
                    ? $"{lobbyCodeMinLength}"
                    : $"de {lobbyCodeMinLength} a {lobbyCodeMaxLength}";

                return $"O código da partida tem {expected} caracteres, e você digitou {code.Length}.";
            }

            foreach (char c in code)
            {
                bool isAsciiLetterOrDigit = c is >= 'A' and <= 'Z' or >= '0' and <= '9';
                if (!isAsciiLetterOrDigit) return "O código da partida só tem letras e números.";
            }

            return null;
        }

        public async void CreateLanGame()
        {
            if (_isBusy || lan == null) return;

            // Reports what is wrong with the port, which lands on the LAN notice.
            if (!lan.ValidateHost()) return;

            SetBusy(true);
            ShowWaiting(lanNotice, "Criando a partida...");

            // Started: the Lobby scene is loading, and the menu stays locked until it is gone.
            if (await lan.StartSessionLanAsync()) return;

            SetBusy(false);
            FailIfStillWaiting(lanNotice, "Não foi possível criar a partida.");
        }

        public async void JoinLanGame()
        {
            if (_isBusy || lan == null) return;

            // Reports what is wrong with the port or the IP, which lands on the LAN notice.
            if (!lan.ValidateJoin()) return;

            string address = lan.JoinAddress;

            SetBusy(true);
            ShowWaiting(lanNotice, $"Conectando a {address}...");

            if (!await lan.JoinSessionLanAsync())
            {
                SetBusy(false);
                FailIfStillWaiting(lanNotice, $"Não foi possível conectar a {address}.");
                return;
            }

            // The attempt is under way. If the host answers it moves us into its Lobby scene; a
            // refusal or a timeout comes back through ConnectionFeedback (PresentConnectionMessage).
            // A wrong IP takes the transport a whole minute to give up, so the player may cancel.
            ShowWaiting(lanNotice, $"Conectando a {address}...", CancelLanJoin);
        }

        private async void CancelLanJoin()
        {
            ShowWaiting(lanNotice, "Cancelando...");

            await NetworkSession.EnsureStoppedAsync();

            HideNotice(lanNotice);
            SetBusy(false);
        }

        /// <summary>
        /// Locks every button of the menu while something is loading, so a second click cannot
        /// start a second lobby, join or session. Also used by the lobby panel.
        /// </summary>
        public void SetBusy(bool busy)
        {
            _isBusy = busy;
            RefreshLock();
        }

        private void RefreshLock()
        {
            if (_lockGroup == null) return;

            // An open notice is modal: only its own button works until it is closed.
            bool noticeOpen = (onlineNotice != null && onlineNotice.IsOpen) || (lanNotice != null && lanNotice.IsOpen);
            _lockGroup.interactable = !_isBusy && !noticeOpen;
        }

        /// <summary>
        /// Connection messages raised while the menu is open (lobby errors, invalid port or IP, a host
        /// that never answered) are shown on the notice of the panel in view instead of the toast,
        /// and the menu unlocks so the player can try again.
        /// </summary>
        private bool PresentConnectionMessage(string message)
        {
            MenuNoticePanel notice = NoticeInView();
            if (notice == null) return false;

            _isBusy = false;
            ShowNotice(notice, message);
            return true;
        }

        private MenuNoticePanel NoticeInView()
        {
            if (lanNotice != null && lanPanel != null && lanPanel.activeInHierarchy) return lanNotice;
            if (onlineNotice != null && onlinePanel != null && onlinePanel.activeInHierarchy) return onlineNotice;
            return null;
        }

        private void ShowNotice(MenuNoticePanel notice, string message)
        {
            if (notice != null) notice.Show(message);
            else ScreenToast.Show(message, ToastKind.Warning);

            RefreshLock();
        }

        private void ShowWaiting(MenuNoticePanel notice, string message, Action onCancel = null)
        {
            if (notice == null) return;

            notice.ShowWaiting(message, onCancel);
            RefreshLock();
        }

        /// <summary>A failure that reported nothing must not leave the notice loading forever.</summary>
        private void FailIfStillWaiting(MenuNoticePanel notice, string message)
        {
            if (notice != null && notice.IsOpen && notice.IsWaiting) ShowNotice(notice, message);
        }

        private void HideNotice(MenuNoticePanel notice)
        {
            if (notice != null) notice.Hide();
            RefreshLock();
        }

        private void HideNotices()
        {
            if (onlineNotice != null) onlineNotice.Hide();
            if (lanNotice != null) lanNotice.Hide();
            RefreshLock();
        }

        public void OpenOnlinePanel()
        {
            HideNotices();
            lanPanel.SetActive(false);
            onlinePanel.SetActive(true);
        }

        public void OpenLanPanel()
        {
            HideNotices();
            onlinePanel.SetActive(false);
            lanPanel.SetActive(true);
        }

        public void ReturnToMenuPanel()
        {
            HideNotices();
            lanPanel.SetActive(false);
            onlinePanel.SetActive(false);
            menuPanel.SetActive(true);
        }

        public void OpenOptionPanel()
        {
            optionPanel.SetActive(true);
        }

        // CRÉDITOS
        public void OpenCreditsPanel()
        {
            creditsPanel.SetActive(true);
        }

        // CRÉDITOS
        public void CloseCreditsPanel()
        {
            creditsPanel.SetActive(false);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

    }
}
