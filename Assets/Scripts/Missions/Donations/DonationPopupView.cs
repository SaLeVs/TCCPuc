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

        [Header("Texto do donate")]
        [SerializeField] private string donationTextFormat = "{donor} donate R$ {amount} para o chat!";

        [Header("Animation")]
        [SerializeField] private float enterDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.25f;
        [SerializeField] private AnimationCurve enterCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public string InstanceId { get; private set; }

        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        public void Setup(DonationNetworkState state)
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

            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            _rect.localScale = Vector3.one * 0.85f;
            StopAllCoroutines();
            StartCoroutine(AnimateEnter());
        }

        public void UpdateState(DonationNetworkState state)
        {
            if (progressFill != null) progressFill.fillAmount = state.Progress;
        }

        /// <summary>
        /// Chamado pelo DonationUiController a cada frame com o quanto falta pra expirar:
        /// ratio (0..1, pra barra) e remainingSeconds (pro texto de contagem regressiva).
        /// Passe remainingSeconds &lt; 0 pra donates que nunca expiram (limpa o texto).
        /// </summary>
        public void SetExpiration(float ratio, float remainingSeconds)
        {
            if (expirationFill != null) expirationFill.fillAmount = Mathf.Clamp01(ratio);

            if (expirationText != null)
            {
                expirationText.text = remainingSeconds >= 0f
                    ? $"{Mathf.CeilToInt(remainingSeconds)}s"
                    : string.Empty;
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