using UnityEngine;
using System.Collections;

public class PanelDissolve : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public float fadeOutDuration = 0.5f;
    public float fadeInDuration = 0.5f;

    private Coroutine currentRoutine;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        // Fica invisível assim que ativa, SEM começar o fade sozinho
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // Chamado manualmente depois que a câmera chegar
    public void Appear()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(FadeTo(1f, fadeInDuration, enableAfter: true));
    }

    public void ClosePanel()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(FadeOutThenDisable());
    }

    IEnumerator FadeOutThenDisable()
    {
        yield return StartCoroutine(FadeTo(0f, fadeOutDuration, enableAfter: false));
        gameObject.SetActive(false);
    }

    IEnumerator FadeTo(float targetAlpha, float duration, bool enableAfter)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
        canvasGroup.interactable = enableAfter;
        canvasGroup.blocksRaycasts = enableAfter;
        currentRoutine = null;
    }
}