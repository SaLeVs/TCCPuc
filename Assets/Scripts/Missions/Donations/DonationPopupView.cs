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

        [Header("Compact mode")]
        [Tooltip("Turned off while the feed is collapsed, leaving the expiration icon alone as the stub.")]
        [SerializeField] private GameObject[] detailObjects;

        [Tooltip("Panel background, hidden with the details so the stub is only the expiration icon.")]
        [SerializeField] private Graphic panelBackground;

        [Tooltip("Size the popup shrinks to while collapsed, so the feed stops holding full-size slots.")]
        [SerializeField] private Vector2 compactSize = new Vector2(60f, 60f);

        [Header("Donate text")]
        [SerializeField] private string donationTextFormat = "{donor} donate R$ {amount} para o chat!";

        [Header("Animation")]
        [SerializeField] private float enterDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.25f;
        [SerializeField] private AnimationCurve enterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public string InstanceId { get; private set; }

        private RectTransform _rect;
        private Vector2 _fullSize;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _fullSize = _rect.sizeDelta;
        }

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

            ApplyIcon(icon);

            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            _rect.localScale = Vector3.one * 0.85f;
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


        /// <summary>
        /// Strips the popup down to its expiration icon, or puts it back. Driven by the HUD panel
        /// that owns the feed, so a popup spawned while collapsed comes up compact too.
        /// </summary>
        public void SetCompact(bool compact)
        {
            if (detailObjects != null)
            {
                foreach (GameObject detail in detailObjects)
                {
                    if (detail != null) detail.SetActive(!compact);
                }
            }

            if (panelBackground != null) panelBackground.enabled = !compact;

            _rect.sizeDelta = compact ? compactSize : _fullSize;
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

            if (expirationText != null)
            {
                expirationText.text = remainingSeconds >= 0f ? $"{Mathf.CeilToInt(remainingSeconds)}s" : string.Empty;
            }
        }

        public void PlayExit(Action onComplete)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateExit(onComplete));
        }

        private IEnumerator AnimateEnter()
        {
            float timer = 0f;

            while (timer < enterDuration)
            {
                timer += Time.deltaTime;
                float p = enterCurve.Evaluate(Mathf.Clamp01(timer / enterDuration));
                canvasGroup.alpha = p;
                _rect.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, p);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            _rect.localScale = Vector3.one;
        }

        private IEnumerator AnimateExit(Action onComplete)
        {
            float timer = 0f;
            float startAlpha = canvasGroup.alpha;

            while (timer < exitDuration)
            {
                timer += Time.deltaTime;
                float p = Mathf.Clamp01(timer / exitDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
                _rect.anchoredPosition += new Vector2(0f, Time.deltaTime * 40f);
                yield return null;
            }

            onComplete?.Invoke();
        }
    }
}