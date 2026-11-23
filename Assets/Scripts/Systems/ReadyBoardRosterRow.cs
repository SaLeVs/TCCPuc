using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems
{
    /// <summary>One line of the cast list on the lobby ready board.</summary>
    public class ReadyBoardRosterRow : MonoBehaviour
    {
        public enum State
        {
            Waiting,
            Ready,
            Connecting,
        }

        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject hostTag;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image statusDot;

        [Header("Colors")]
        [SerializeField] private Color nameColor = new Color(0.93f, 0.89f, 0.82f);
        [SerializeField] private Color localNameColor = new Color(1f, 0.97f, 0.88f);
        [SerializeField] private Color mutedColor = new Color(0.55f, 0.5f, 0.47f);
        [SerializeField] private Color readyColor = new Color(0.55f, 0.82f, 0.45f);
        [SerializeField] private Color waitingColor = new Color(0.86f, 0.62f, 0.3f);

        [Header("Texts")]
        [SerializeField] private string readyLabel = "Pronto";
        [SerializeField] private string waitingLabel = "Aguardando";
        [SerializeField] private string connectingLabel = "Conectando...";
        [SerializeField] private string localSuffix = " <size=75%><alpha=#99>(você)</size>";
        [Tooltip("Appended to the host's name when there is no separate host tag object.")]
        [SerializeField] private string hostSuffix = "  <size=62%><color=#D2353B>HOST</color></size>";

        public void Set(string playerName, bool isHost, bool isLocal, State state)
        {
            bool connecting = state == State.Connecting;
            string hostMark = isHost && hostTag == null ? hostSuffix : string.Empty;

            nameText.text = connecting
                ? connectingLabel
                : playerName + (isLocal ? localSuffix : string.Empty) + hostMark;
            nameText.color = connecting ? mutedColor : isLocal ? localNameColor : nameColor;
            nameText.fontStyle = connecting ? FontStyles.Italic : FontStyles.Normal;

            if (hostTag != null) hostTag.SetActive(isHost && !connecting);

            Color stateColor = state switch
            {
                State.Ready => readyColor,
                State.Waiting => waitingColor,
                _ => mutedColor,
            };

            statusText.text = state switch
            {
                State.Ready => readyLabel,
                State.Waiting => waitingLabel,
                _ => "-",
            };
            statusText.color = stateColor;

            if (statusDot != null) statusDot.color = stateColor;
        }
    }
}
