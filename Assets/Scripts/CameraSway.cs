using UnityEngine;

public class CameraSway : MonoBehaviour
{
    [Header("Velocidade do Balanço")]
    [Tooltip("Quanto maior, mais rápido a câmera se mexe")]
    public float speed = 0.8f;

    [Header("Intensidade do Movimento")]
    [Tooltip("Mantenha valores bem baixos para o efeito ser sutil (ex: 0.02 a 0.05)")]
    public float amountX = 0.03f;
    public float amountY = 0.03f;

    private Vector3 initialPosition;

    void Start()
    {
        // Salva a posição original da câmera
        initialPosition = transform.localPosition;
    }

    void Update()
    {
        // Calcula o deslocamento suave usando Perlin Noise
        float offsetX = (Mathf.PerlinNoise(Time.time * speed, 0f) - 0.5f) * 2f * amountX;
        float offsetY = (Mathf.PerlinNoise(0f, Time.time * speed) - 0.5f) * 2f * amountY;

        // Aplica o movimento em relação à posição inicial
        transform.localPosition = initialPosition + new Vector3(offsetX, offsetY, 0);
    }
}