using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Missions.Donations
{
    public class DonationPopupView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text donationText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Image expirationFill;
        [SerializeField] private TMP_Text expirationText;
        [SerializeField] private Image donationIcon;

        [Header("Icon")]
        [Tooltip("Used when the DonationDefinition has no icon assigned. Leave empty and the icon object is hidden instead.")]
        [SerializeField] private Sprite fallbackIcon;

        [Header("Donate text")]
        [SerializeField] private string donationTextFormat = "{donor} donate R$ {amount} para o chat!";

        [Header("Animation")]
        [SerializeField] private float enterDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.25f;
        [SerializeField] private AnimationCurve enterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public string InstanceId { get; private set; }

        private RectTransform _rect;

        // Last whole second written to the timer. SetExpiration runs every frame; the label only
        // changes once a second, so rebuilding its string and mesh in between was wasted work.
        private int _shownSeconds = int.MinValue;

        /// <summary>
        /// Resolved on first use instead of in Awake. A chip is instantiated into a tray that may
        /// still be switched off, and Unity does not run Awake until an object is active in the
        /// hierarchy - so caching here in Awake left this null exactly when it was needed.
        /// </summary>
        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        public void Setup(DonationNetworkState state, Sprite icon = null)
        {
            InstanceId = state.InstanceId.ToString();
            
            if (donationText != null)
            {
                donationText.text = donationTextFormat.Replace("{donor}", state.DonorName.ToString()).Replace("{amount}", state.Amount.ToString("0.00"));
            }

            if (messageText != null) messageText.text = state.Message.ToString();
            if (progressFill != null) progressFill.fillAmount = state.Progress;
            if (expirationFill != null) expirationFill.fillAmount = 1f;
            if (expirationText != null) expirationText.text = string.Empty;
            _shownSeconds = int.MinValue;

            ApplyIcon(icon);

            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            Rect.localScale = Vector3.one * 0.85f;
            StopAllCoroutines();
            StartCoroutine(AnimateEnter());
        }

        /// <summary>
        /// Shows the sprite authored on the DonationDefinition, falling back to <see cref="fallbackIcon"/>.
        /// With neither, the icon object is turned off so the layout doesn't keep a blank square.
        /// </summary>
        private void ApplyIcon(Sprite icon)
        {
            if (donationIcon == null) return;

            Sprite sprite = icon != null ? icon : fallbackIcon;

            donationIcon.gameObject.SetActive(sprite != null);
            if (sprite != null) donationIcon.sprite = sprite;
        }


        public void UpdateState(DonationNetworkState state)
        {
            if (progressFill != null) progressFill.fillAmount = state.Progress;
        }

        /// <summary>
        /// Called by the DonationUiController each frame to update the expiration bar and countdown text.
        /// ratio (0..1, for bar) and remainingSeconds (for the countdown text).
        /// Pass remainingSeconds &lt; 0 for donations that never expire (clears the text).
        /// </summary>
        public void SetExpiration(float ratio, float remainingSeconds)
        {
            if (expirationFill != null) expirationFill.fillAmount = Mathf.Clamp01(ratio);

            if (expirationText == null) return;

            int seconds = remainingSeconds >= 0f ? Mathf.CeilToInt(remainingSeconds) : -1;
            if (seconds == _shownSeconds) return;

            _shownSeconds = seconds;

            if (seconds < 0) expirationText.SetText(string.Empty);
            else expirationText.SetText("{0}s", seconds);
        }

        /// <summary>What still has to run once this chip has finished leaving. Null when nothing is.</summary>
        private Action _pendingExit;

        public void PlayExit(Action onComplete)
        {
            StopAllCoroutines();

            _pendingExit = onComplete;

            // Nothing to animate on an object that is already switched off, and StartCoroutine
            // would refuse anyway - finish straight away so the caller still gets its callback.
            if (!isActiveAndEnabled)
            {
                CompleteExit();
                return;
            }

            StartCoroutine(AnimateExit());
        }

        /// <summary>
        /// Finishes an exit that was cut short.
        ///
        /// <para>The tray root is switched off the moment its last chip is dropped, which leaves
        /// this object inactive in the hierarchy and makes Unity stop its coroutines part way
        /// through the fade. The completion callback is what destroys the chip, so without running
        /// it here the object survived - parented to the tray, half faded, and already forgotten by
        /// the controller, which had dropped its reference. Every emptied tray left one behind.</para>
        /// </summary>
        private void OnDisable()
        {
            if (_pendingExit != null) CompleteExit();
        }

        private void CompleteExit()
        {
            Action callback = _pendingExit;

            _pendingExit = null;

            callback?.Invoke();
        }

        private IEnumerator AnimateEnter()
        {
            float timer = 0f;

            while (timer < enterDuration)
            {
                timer += Time.deltaTime;
                float p = enterCurve.Evaluate(Mathf.Clamp01(timer / enterDuration));
                canvasGroup.alpha = p;
                Rect.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, p);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            Rect.localScale = Vector3.one;
        }

        private IEnumerator AnimateExit()
        {
            float timer = 0f;
            float startAlpha = canvasGroup.alpha;

            while (timer < exitDuration)
            {
                timer += Time.deltaTime;
                float p = Mathf.Clamp01(timer / exitDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
                Rect.anchoredPosition += new Vector2(0f, Time.deltaTime * 40f);
                yield return null;
            }

            CompleteExit();
        }
    }
}