using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Audience
{
    public class AudienceBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private float fillLerpSpeed = 6f;
        
        [SerializeField] private TextMeshProUGUI percentageText; 
        [SerializeField] private TextMeshProUGUI audienceText;
        
        private bool _initialized;
        private Coroutine _fillRoutine;
        
        
        private void Start()
        {
            if (AudienceManager.Instance == null)
            {
                Debug.LogWarning("AudienceBar: AudienceManager not found. Bar disabled.");
                gameObject.SetActive(false);
                return;
            }

            _initialized = true;
            fillImage.fillAmount = AudienceManager.Instance.NormalizedAudience;
 
            RefreshLabels(AudienceManager.Instance.CurrentAudience, AudienceManager.Instance.MaxAudience);
 
            AudienceManager.Instance.OnAudienceChanged += AudienceManager_OnAudienceChanged;
        }
        
        
        private void AudienceManager_OnAudienceChanged(float newValue)
        {
            RefreshLabels(newValue, AudienceManager.Instance.MaxAudience);
            AnimateFill(AudienceManager.Instance.NormalizedAudience);
        }
        
        private void RefreshLabels(float current, float max)
        {
            percentageText.text = $"{AudienceManager.Instance.NormalizedAudience * 100f:F0}%";
            audienceText.text = $"{current:F0} / {max:F0}";
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
        
        
        private void OnDestroy()
        {
            if (!_initialized || AudienceManager.Instance == null) return;
            
            AudienceManager.Instance.OnAudienceChanged -= AudienceManager_OnAudienceChanged;
        }
        
    }
}

