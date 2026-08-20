using UnityEngine;

public class FloatingCard : MonoBehaviour
{
    [Header("Flutuação (sobe e desce)")]
    public float floatAmplitude = 15f;
    public float floatSpeed = 1.5f;

    [Header("Balanço (nunca vira de verdade)")]
    public float tiltAmountY = 12f;   // graus máximos de "vira de lado" (efeito 3D sutil)
    public float tiltAmountZ = 4f;    // graus máximos de inclinação lateral
    public float tiltSpeed = 1f;

    private RectTransform rectTransform;
    private Vector2 startPos;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        rectTransform.pivot = new Vector2(0.5f, 0.5f); // garante centro
        startPos = rectTransform.anchoredPosition;
    }

    void Update()
    {
        // Flutuação vertical suave
        float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        rectTransform.anchoredPosition = new Vector2(startPos.x, newY);

        // Balanço suave (vai e volta, nunca completa a volta)
        float tiltY = Mathf.Sin(Time.time * tiltSpeed) * tiltAmountY;
        float tiltZ = Mathf.Sin(Time.time * tiltSpeed * 0.7f) * tiltAmountZ; // velocidade um pouco diferente pra parecer mais orgânico

        rectTransform.localRotation = Quaternion.Euler(0f, tiltY, tiltZ);
    }
}