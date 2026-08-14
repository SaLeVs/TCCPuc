using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ButtonDissolveOnClick : MonoBehaviour
{
    [Header("Referências")]
    public Button button;
    public TMP_Text buttonText;

    [Header("Configuração do fade")]
    public float fadeOutDuration = 0.5f;

    private Coroutine currentRoutine;
    private Color originalColor;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (buttonText == null) buttonText = GetComponentInChildren<TMP_Text>();

        originalColor = buttonText.color;
    }

    void OnEnable()
    {
        // Sempre que esse objeto (ou o painel dele) for reativado, reseta o estado
        ResetButton();
    }

    public void OnButtonClick()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(DissolveRoutine());
    }

    IEnumerator DissolveRoutine()
    {
        button.interactable = false; // não dá mais pra clicar

        float elapsed = 0f;
        float startAlpha = buttonText.color.a;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;

            Color c = buttonText.color;
            c.a = Mathf.Lerp(startAlpha, 0f, t);
            buttonText.color = c;

            yield return null;
        }

        Color finalColor = buttonText.color;
        finalColor.a = 0f;
        buttonText.color = finalColor;

        currentRoutine = null;
    }

    public void ResetButton()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        buttonText.color = originalColor;
        button.interactable = true;
    }
}