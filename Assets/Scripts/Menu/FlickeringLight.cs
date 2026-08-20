using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Light))]
public class FlickeringLightBadContact : MonoBehaviour
{
    private Light lightSource;

    [Header("Intensidade")]
    public float steadyIntensity = 2f;
    public float minFlickerIntensity = 0.1f;
    public float maxFlickerIntensity = 2f;

    [Header("Duração de cada fase (segundos)")]
    public float minSteadyTime = 5f;
    public float maxSteadyTime = 10f;
    public float minFlickerTime = 1f;
    public float maxFlickerTime = 3f;

    [Header("Velocidade do piscar durante o mau contato")]
    public float flickerChangeSpeed = 0.05f; // menor = pisca mais rápido/errático

    void Start()
    {
        lightSource = GetComponent<Light>();
        StartCoroutine(LightRoutine());
    }

    IEnumerator LightRoutine()
    {
        while (true)
        {
            // Fase estável
            lightSource.intensity = steadyIntensity;
            float steadyDuration = Random.Range(minSteadyTime, maxSteadyTime);
            yield return new WaitForSeconds(steadyDuration);

            // Fase de mau contato (piscando)
            float flickerDuration = Random.Range(minFlickerTime, maxFlickerTime);
            float flickerTimer = 0f;

            while (flickerTimer < flickerDuration)
            {
                lightSource.intensity = Random.Range(minFlickerIntensity, maxFlickerIntensity);
                yield return new WaitForSeconds(flickerChangeSpeed);
                flickerTimer += flickerChangeSpeed;
            }
        }
    }
}