using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Tela de loading: fade in -> fica X segundos com efeito VHS -> fade out -> troca de cena.
/// Usa tempo "unscaled" para funcionar mesmo se o Time.timeScale for 0.
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    [Header("Cena")]
    [SerializeField] private string nextSceneName = "Gameplay";
    [SerializeField] private float screenDuration = 5f;

    [Header("Fade (CanvasGroup de uma Image preta em tela cheia)")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeInDuration = 0.6f;
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("UI")]
    [SerializeField] private TMP_Text recText;      // "REC ● 00:00:03"
    [SerializeField] private TMP_Text statusText;   // frases que trocam
    [SerializeField] private float messageInterval = 1.2f;
    [SerializeField]
    private string[] statusMessages =
    {
        "AJUSTANDO AS LUZES...",
        "CHECANDO O MICROFONE...",
        "PREPARANDO A TRANSMISSAO...",
        "QUASE NO AR..."
    };

    [Header("Glitch VHS")]
    [SerializeField] private Vector2 glitchIntervalStart = new Vector2(1.2f, 2.5f); // no comeco
    [SerializeField] private Vector2 glitchIntervalEnd = new Vector2(0.3f, 0.8f);   // perto do fim
    [SerializeField] private bool glitchAtStart = true;
    [SerializeField] private UnityEvent onGlitch;   // ligue aqui o TriggerGlitch() do seu VHS

    private IEnumerator Start()
    {
        // Comeca a carregar a proxima cena em segundo plano, mas sem ativar ainda.
        // Sem cena (campo vazio), a tela so faz o efeito: outra parte do jogo troca de cena.
        AsyncOperation op = null;
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                op = SceneManager.LoadSceneAsync(nextSceneName);
                op.allowSceneActivation = false;
            }
            else
            {
                Debug.LogWarning($"LoadingScreenController: a cena '{nextSceneName}' não está na build; a tela só mostra o efeito.", this);
            }
        }

        // Tela comeca preta e faz fade in
        SetFade(1f);
        yield return Fade(1f, 0f, fadeInDuration);

        if (glitchAtStart) TriggerGlitch();

        // Fica na tela pelo tempo definido
        float t = 0f;
        float nextGlitch = RandomInterval(0f);
        float nextMessage = 0f;
        int messageIndex = 0;

        while (t < screenDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / screenDuration);

            UpdateRec(t);

            // Glitch em intervalos aleatorios, cada vez mais frequentes
            if (t >= nextGlitch)
            {
                TriggerGlitch();
                nextGlitch = t + RandomInterval(progress);
            }

            // Troca a frase de status
            if (statusText != null && statusMessages.Length > 0 && t >= nextMessage)
            {
                statusText.text = statusMessages[messageIndex % statusMessages.Length];
                messageIndex++;
                nextMessage = t + messageInterval;
            }

            yield return null;
        }

        if (op == null) yield break;

        // Garante que a cena ja terminou de carregar (o progress para em 0.9)
        while (op.progress < 0.9f)
            yield return null;

        // Fade out e troca de cena
        yield return Fade(0f, 1f, fadeOutDuration);
        op.allowSceneActivation = true;
    }

    private float RandomInterval(float progress)
    {
        float min = Mathf.Lerp(glitchIntervalStart.x, glitchIntervalEnd.x, progress);
        float max = Mathf.Lerp(glitchIntervalStart.y, glitchIntervalEnd.y, progress);
        return Random.Range(min, max);
    }

    private void TriggerGlitch()
    {
        onGlitch?.Invoke();
    }

    private void UpdateRec(float t)
    {
        if (recText == null) return;

        int total = Mathf.FloorToInt(t);
        int min = total / 60;
        int sec = total % 60;

        // Bolinha vermelha piscando duas vezes por segundo
        bool dotOn = Mathf.FloorToInt(t * 2f) % 2 == 0;
        string dotColor = dotOn ? "#FF2A2A" : "#FF2A2A00";

        recText.text = $"REC <color={dotColor}>●</color> 00:{min:00}:{sec:00}";
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeGroup == null || duration <= 0f)
        {
            SetFade(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFade(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetFade(to);
    }

    private void SetFade(float alpha)
    {
        if (fadeGroup == null) return;
        fadeGroup.alpha = alpha;
        fadeGroup.blocksRaycasts = alpha > 0.01f;
    }
}