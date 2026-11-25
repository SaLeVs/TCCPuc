using System.Collections;
using System.Collections.Generic;
using Enums;
using Network;
using Systems;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ui.EndScreen
{
    /// <summary>
    /// The Win/Lose scene: reads <see cref="MatchResult"/>, fills the panel (recording time and, on a
    /// victory, how many got out), hands the names to the cast and fades the whole thing in.
    ///
    /// <para>Opened straight from the Editor (no match behind it) it makes up a result, so the
    /// scene can be looked at and tweaked on its own.</para>
    /// </summary>
    public class EndScreenController : MonoBehaviour
    {
        [SerializeField] private bool isVictoryScene;

        [Header("Panel")]
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text timeNumber;
        [SerializeField] private TMP_Text survivorsNumber;
        [Tooltip("Hidden on a defeat: nobody to count.")]
        [SerializeField] private GameObject[] survivorsOnly;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 1.6f;
        [Tooltip("The scene plays a little on its own before the panel shows up.")]
        [SerializeField] private float panelDelay = 2.2f;
        [SerializeField] private float panelFadeDuration = 0.8f;
        [Tooltip("How long the time counter takes to roll up to the real value.")]
        [SerializeField] private float countUpDuration = 1.2f;

        [Header("Cast")]
        [SerializeField] private EndScreenCast cast;

        [Header("Editor preview (no match behind the scene)")]
        [SerializeField] private string[] previewPlayers = { "Guilherme", "Ana", "Jorge", "Igor" };
        [SerializeField] private int previewSurvivors = 2;
        [SerializeField] private float previewDuration = 754f;

        private bool _isReturningToMenu;

        private void Start()
        {
            CursorState.Hold(isVictoryScene ? CursorReason.Victory : CursorReason.GameOver);

            if (!MatchResult.HasValue) MakePreviewResult();

            // Everyone keeps hearing everyone on the end screen: the match's positional channel has no
            // player objects left to carry the voices.
            if (VivoxManager.instance != null) VivoxManager.instance.EnterEndScreenVoice();

            List<string> survivors = new List<string>(MatchResult.Survivors);
            List<string> players = new List<string>(MatchResult.Players);

            if (survivorsNumber != null) survivorsNumber.text = $"[{survivors.Count}/{players.Count}]";
            if (!isVictoryScene)
            {
                foreach (GameObject go in survivorsOnly)
                {
                    if (go != null) go.SetActive(false);
                }
            }

            if (timeNumber != null) timeNumber.text = $"[{MatchResult.FormatDuration(0f)}]";

            if (cast != null)
            {
                cast.Populate(isVictoryScene ? survivors : players);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }

            ScreenFade.Create(1f).FadeTo(0f, fadeInDuration, 0.2f);
            StartCoroutine(RevealPanel());
        }

        private void MakePreviewResult()
        {
            List<string> players = new List<string>(previewPlayers);
            int survivorCount = isVictoryScene ? Mathf.Clamp(previewSurvivors, 1, players.Count) : 0;
            MatchResult.Set(isVictoryScene, previewDuration, players.GetRange(0, survivorCount), players);
        }

        private IEnumerator RevealPanel()
        {
            if (panel == null) yield break;

            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;

            yield return new WaitForSeconds(panelDelay);

            float time = 0f;
            while (time < panelFadeDuration)
            {
                time += Time.deltaTime;
                panel.alpha = Mathf.SmoothStep(0f, 1f, time / panelFadeDuration);
                yield return null;
            }

            panel.alpha = 1f;
            panel.interactable = true;
            panel.blocksRaycasts = true;

            if (timeNumber == null) yield break;

            time = 0f;
            while (time < countUpDuration)
            {
                time += Time.deltaTime;
                float shown = MatchResult.DurationSeconds * Mathf.SmoothStep(0f, 1f, time / countUpDuration);
                timeNumber.text = $"[{MatchResult.FormatDuration(shown)}]";
                yield return null;
            }

            timeNumber.text = $"[{MatchResult.FormatDuration(MatchResult.DurationSeconds)}]";
        }

        public void ReturnToMainMenu()
        {
            if (_isReturningToMenu) return;
            _isReturningToMenu = true;

            // The same way out as the pause menu. Shutting the session down alone left this player in
            // the match's voice channel and in its lobby, still marked as having started it - so the
            // next online match never started (the host's StartGame refused, the clients stopped
            // polling for the start).
            try
            {
                if (VivoxManager.instance != null) VivoxManager.instance.LeaveVoiceChannel();
                if (Lobby.instance != null) Lobby.instance.LeaveLobby();
                HostSingleton.instance?.gameManager?.Dispose();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            MatchResult.Clear();
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
