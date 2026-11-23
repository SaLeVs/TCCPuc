using System.Collections.Generic;
using ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems
{
    /// <summary>
    /// Draws the lobby ready board: the contract poster and sheet, the escape objectives, the cast
    /// with who is ready, the ready button and the countdown. Holds no state of its own;
    /// <see cref="ReadyBoard"/> feeds it.
    /// </summary>
    public class ReadyBoardView : MonoBehaviour
    {
        public struct RosterEntry
        {
            public string Name;
            public bool IsHost;
            public bool IsLocal;
            public ReadyBoardRosterRow.State State;
        }

        [Header("Contract picker")]
        [SerializeField] private Image posterImage;
        [Tooltip("Shadow/frame behind the poster, hidden with it when the contract has no poster.")]
        [SerializeField] private GameObject posterShadow;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text contractIndexText;
        [SerializeField] private Transform pageDotsContainer;
        [SerializeField] private Image pageDotTemplate;
        [SerializeField] private TMP_Text pickerHintText;

        [Header("Contract sheet")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image[] difficultyPips;
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private TMP_Text audienceGoalText;

        [Header("Objectives")]
        [SerializeField] private TMP_Text mainMissionText;
        [SerializeField] private TMP_Text audienceObjectiveText;
        [SerializeField] private TMP_Text escapeObjectiveText;

        [Header("Cast")]
        [SerializeField] private Transform rosterContainer;
        [SerializeField] private ReadyBoardRosterRow rosterRowTemplate;
        [SerializeField] private TMP_Text readyCountText;

        [Header("Ready")]
        [SerializeField] private Image readyButtonImage;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private GameObject countdownRoot;
        [SerializeField] private TMP_Text countdownText;

        [Header("Colors")]
        [SerializeField] private Color pipOnColor = new Color(0.78f, 0.12f, 0.14f);
        [SerializeField] private Color pipOffColor = new Color(0.25f, 0.2f, 0.18f, 0.35f);
        [SerializeField] private Color dotOnColor = new Color(0.78f, 0.12f, 0.14f);
        [SerializeField] private Color dotOffColor = new Color(0.93f, 0.89f, 0.82f, 0.55f);
        [SerializeField] private Color readyButtonColor = new Color(0.62f, 0.09f, 0.11f);
        [SerializeField] private Color cancelButtonColor = new Color(0.22f, 0.19f, 0.17f);
        [SerializeField, Range(0f, 1f)] private float disabledArrowAlpha = 0.3f;

        [Header("Texts")]
        [SerializeField] private string[] difficultyNames = { "Fácil", "Média", "Difícil" };
        [SerializeField] private string audienceObjectiveFormat = "Atinjam <b>{0}</b> de audiência";
        [SerializeField] private string audienceObjectiveHint = "Gravem objetos de cena e o monstro para atrair público";
        [SerializeField] private string escapeObjective = "Coloquem os disquetes no totem e fujam do teatro";
        [SerializeField] private string hostPickHint = "Use as setas para escolher o contrato";
        [SerializeField] private string hostOnlyContractHint = "Só há este contrato por enquanto";
        [SerializeField] private string clientPickHint = "O host escolhe o contrato";
        [SerializeField] private string readyLabel = "Estou pronto";
        [SerializeField] private string cancelLabel = "Cancelar";
        [SerializeField] private string readyCountFormat = "{0} de {1} prontos";
        [SerializeField] private string countdownFormat = "A cortina abre em <size=160%>{0}</size>";

        private readonly List<ReadyBoardRosterRow> _rows = new();
        private readonly List<Image> _dots = new();
        private int _lastCountdownSecond = int.MinValue;

        private void Awake()
        {
            if (rosterRowTemplate != null) rosterRowTemplate.gameObject.SetActive(false);
            if (pageDotTemplate != null) pageDotTemplate.gameObject.SetActive(false);
            if (countdownRoot != null) countdownRoot.SetActive(false);
        }

        public void ShowContract(ContractsSO contract, int index, int count, bool canPick)
        {
            bool several = count > 1;

            SetArrow(previousButton, canPick, several);
            SetArrow(nextButton, canPick, several);

            contractIndexText.text = count > 0 ? $"Contrato {index + 1} de {count}" : string.Empty;
            pickerHintText.text = !canPick ? clientPickHint : several ? hostPickHint : hostOnlyContractHint;
            BuildDots(index, count);

            if (contract == null)
            {
                posterImage.enabled = false;
                titleText.text = "Sem contrato";
                subtitleText.text = descriptionText.text = audienceGoalText.text = string.Empty;
                mainMissionText.text = audienceObjectiveText.text = escapeObjectiveText.text = string.Empty;
                return;
            }

            posterImage.enabled = contract.poster != null;
            if (posterShadow != null) posterShadow.SetActive(contract.poster != null);
            posterImage.sprite = contract.poster;

            titleText.text = contract.contractName;
            subtitleText.text = contract.posterTitle;
            descriptionText.text = contract.description;

            int difficulty = Mathf.Clamp(contract.difficulty, 1, difficultyPips.Length);
            for (int i = 0; i < difficultyPips.Length; i++)
            {
                difficultyPips[i].color = i < difficulty ? pipOnColor : pipOffColor;
            }
            difficultyText.text = difficultyNames[Mathf.Clamp(difficulty - 1, 0, difficultyNames.Length - 1)];

            string goal = FormatAudience(contract.maxAudience);
            audienceGoalText.text = goal;

            MissionSO main = contract.mainMission;
            mainMissionText.text = main != null
                ? $"Missão principal: <b>{main.missionName}</b>\n<size=78%><alpha=#B0>{main.instructions}</size>"
                : "Missão principal";
            audienceObjectiveText.text = string.Format(audienceObjectiveFormat, goal) +
                                         $"\n<size=78%><alpha=#B0>{audienceObjectiveHint}</size>";
            escapeObjectiveText.text = escapeObjective;
        }

        public void ShowRoster(IReadOnlyList<RosterEntry> entries, int readyCount, int expected)
        {
            while (_rows.Count < entries.Count)
            {
                ReadyBoardRosterRow row = Instantiate(rosterRowTemplate, rosterContainer);
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < entries.Count;
                _rows[i].gameObject.SetActive(used);

                if (used)
                {
                    RosterEntry e = entries[i];
                    _rows[i].Set(e.Name, e.IsHost, e.IsLocal, e.State);
                }
            }

            readyCountText.text = string.Format(readyCountFormat, readyCount, Mathf.Max(expected, entries.Count));
        }

        public void ShowLocalReady(bool isReady)
        {
            readyButtonText.text = isReady ? cancelLabel : readyLabel;
            readyButtonImage.color = isReady ? cancelButtonColor : readyButtonColor;
        }

        /// <param name="secondsLeft">Negative hides the countdown.</param>
        public void ShowCountdown(float secondsLeft)
        {
            bool show = secondsLeft >= 0f;

            if (countdownRoot.activeSelf != show) countdownRoot.SetActive(show);
            if (!show)
            {
                _lastCountdownSecond = int.MinValue;
                return;
            }

            int second = Mathf.CeilToInt(secondsLeft);
            if (second == _lastCountdownSecond) return;

            _lastCountdownSecond = second;
            countdownText.text = string.Format(countdownFormat, Mathf.Max(second, 0));
        }

        /// <summary>Hidden for clients; dimmed for the host while there's nothing to switch to.</summary>
        private void SetArrow(Button arrow, bool visible, bool usable)
        {
            arrow.gameObject.SetActive(visible);
            arrow.interactable = usable;

            if (arrow.TryGetComponent(out CanvasGroup group))
            {
                group.alpha = usable ? 1f : disabledArrowAlpha;
            }
        }

        private void BuildDots(int index, int count)
        {
            if (pageDotTemplate == null) return;

            while (_dots.Count < count)
            {
                Image dot = Instantiate(pageDotTemplate, pageDotsContainer);
                _dots.Add(dot);
            }

            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].gameObject.SetActive(i < count);
                _dots[i].color = i == index ? dotOnColor : dotOffColor;
            }
        }

        private static string FormatAudience(float value)
        {
            // "1.200", pt-BR grouping without depending on culture data being in the build.
            return Mathf.RoundToInt(value).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        }
    }
}
