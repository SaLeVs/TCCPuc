using System.Collections;
using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Audience
{
    /// <summary>
    /// The number and the fill on the HUD.
    /// </summary>
    /// <remarks>
    /// Binding used to happen once, from <c>Start</c>, while <c>OnDisable</c> unsubscribed. Nothing
    /// ever put the subscription back, so the first time anything switched this object off and on
    /// again the readout froze on whatever it last showed - for the rest of the match, while the
    /// real audience carried on moving underneath it. Disabling it before the manager had spawned
    /// was worse: Unity killed the waiting coroutine and no bind was ever attempted again.
    /// </remarks>
    public class AudienceBar : MonoBehaviour
    {
        [SerializeField] private GameObject audienceBarUi;
        [SerializeField] private Image fillImage;
        [SerializeField] private float fillLerpSpeed = 6f;

        [SerializeField] private TextMeshProUGUI audienceText;

        private bool _startHasRun;
        private bool _isGameScene;
        private bool _subscribed;

        private Coroutine _bindRoutine;
        private Coroutine _fillRoutine;


        private void Start()
        {
            _startHasRun = true;
            _isGameScene = SceneManager.GetActiveScene().name == nameof(Scenes.Game);

            audienceBarUi.SetActive(_isGameScene);

            if (_isGameScene) Bind();
        }

        private void OnEnable()
        {
            // The very first OnEnable runs before Start, which is what decides whether this scene
            // has an audience at all. Start does the first bind.
            if (!_startHasRun || !_isGameScene) return;

            Bind();
        }

        private void Bind()
        {
            if (_subscribed || _bindRoutine != null) return;

            _bindRoutine = StartCoroutine(BindWhenManagerExists());
        }

        private IEnumerator BindWhenManagerExists()
        {
            // Instance is set in Awake, but the ceiling it normalises against is only resolved
            // in OnNetworkSpawn. Binding in between snapped the fill to zero over zero, which a
            // late joiner reads as an empty bar over a match already well under way.
            while (AudienceManager.Instance == null || AudienceManager.Instance.MaxAudience <= 0f)
            {
                yield return null;
            }

            _bindRoutine = null;

            AudienceManager manager = AudienceManager.Instance;

            _subscribed = true;
            manager.OnAudienceChanged += AudienceManager_OnAudienceChanged;

            // Snapped, not animated: on a re-bind the value may have travelled a long way while
            // this was hidden, and lerping up from a stale fill would read as a live gain.
            fillImage.fillAmount = manager.NormalizedAudience;

            RefreshLabels(manager.CurrentAudience);
        }

        private void AudienceManager_OnAudienceChanged(float newAudience)
        {
            RefreshLabels(newAudience);
            AnimateFill(AudienceManager.Instance.NormalizedAudience);
        }

        private void RefreshLabels(float currentAudience)
        {
            audienceText.text = $"{currentAudience:F0}";
        }

        private void AnimateFill(float targetFill)
        {
            if (_fillRoutine != null)
            {
                StopCoroutine(_fillRoutine);
            }

            _fillRoutine = StartCoroutine(FillRoutine(targetFill));
        }

        private IEnumerator FillRoutine(float targetFill)
        {
            while (Mathf.Abs(fillImage.fillAmount - targetFill) > 0.001f)
            {
                fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, targetFill, Time.deltaTime * fillLerpSpeed);
                yield return null;
            }

            fillImage.fillAmount = targetFill;
            _fillRoutine = null;
        }


        private void OnDisable()
        {
            // Unity stops a disabled object's coroutines but leaves these handles pointing at them,
            // so they are dropped here - otherwise the re-bind above would take a dead routine for
            // a running one and never start a new wait.
            _bindRoutine = null;
            _fillRoutine = null;

            if (!_subscribed) return;

            _subscribed = false;

            if (AudienceManager.Instance == null) return;

            AudienceManager.Instance.OnAudienceChanged -= AudienceManager_OnAudienceChanged;
        }

    }
}
