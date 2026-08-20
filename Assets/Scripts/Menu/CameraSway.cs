using UnityEngine;

public class CameraSway : MonoBehaviour
{
    [Header("Velocidade do Balanço")]
    public float speed = 0.8f;

    [Header("Intensidade do Movimento")]
    public float amountX = 0.03f;
    public float amountY = 0.03f;

    private Vector3 basePosition;
    private Vector3 lastOffset;

    void Start()
    {
        basePosition = transform.localPosition;
        lastOffset = Vector3.zero;
    }

    void Update()
    {
        // Verifica se a posição atual bate com o que o sway esperava
        // Se não bater, significa que outro script (tipo o de mover câmera) mexeu nela
        Vector3 expectedPosition = basePosition + lastOffset;
        if (Vector3.Distance(transform.localPosition, expectedPosition) > 0.001f)
        {
            // Atualiza o "centro" do balanço pra acompanhar a nova posição
            basePosition = transform.localPosition - lastOffset;
        }

        float offsetX = (Mathf.PerlinNoise(Time.time * speed, 0f) - 0.5f) * 2f * amountX;
        float offsetY = (Mathf.PerlinNoise(0f, Time.time * speed) - 0.5f) * 2f * amountY;
        lastOffset = new Vector3(offsetX, offsetY, 0);

        transform.localPosition = basePosition + lastOffset;
    }
}