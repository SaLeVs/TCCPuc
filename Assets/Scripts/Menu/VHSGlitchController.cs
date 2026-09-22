using System.Collections;
using UnityEngine;

// Não precisa estar na câmera — pode ficar em qualquer GameObject da cena
// (ex: um objeto vazio chamado "VHSController"). O efeito em si é aplicado
// pela Full Screen Pass Renderer Feature do URP, configurada no Renderer Data
// asset com o material que usa o shader VHSEffectURP.
public class VHSGlitchController : MonoBehaviour
{
    public static VHSGlitchController Instance { get; private set; }

    [Header("Material (arraste aqui o material que usa o shader VHSEffectURP)")]
    public Material vhsMaterial;

    [Header("Efeito sutil (idle)")]
    [Range(0, 1)] public float noiseIntensity = 0.03f;
    [Range(0, 1)] public float scanlineIntensity = 0.08f;
    public float scanlineCount = 800f;
    [Range(0, 0.02f)] public float chromaticAberration = 0.0015f;
    [Range(0, 1)] public float vignetteIntensity = 0.25f;
    public Color colorTint = new Color(1f, 0.95f, 0.9f, 1f);
    [Range(0, 0.05f)] public float trackingWobble = 0.002f;

    [Header("Glitch (quando o monstro toca o player)")]
    public float glitchDuration = 0.6f;
    public AnimationCurve glitchCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private float currentGlitch;
    private Coroutine glitchRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (vhsMaterial == null) return;

        vhsMaterial.SetFloat("_NoiseIntensity", noiseIntensity);
        vhsMaterial.SetFloat("_ScanlineIntensity", scanlineIntensity);
        vhsMaterial.SetFloat("_ScanlineCount", scanlineCount);
        vhsMaterial.SetFloat("_ChromaticAberration", chromaticAberration);
        vhsMaterial.SetFloat("_VignetteIntensity", vignetteIntensity);
        vhsMaterial.SetColor("_ColorTint", colorTint);
        vhsMaterial.SetFloat("_TrackingWobble", trackingWobble);
        vhsMaterial.SetFloat("_GlitchIntensity", currentGlitch);
    }

    /// <summary>Chame quando o monstro encostar no player: VHSGlitchController.Instance.TriggerGlitch();</summary>
    public void TriggerGlitch()
    {
        if (glitchRoutine != null) StopCoroutine(glitchRoutine);
        glitchRoutine = StartCoroutine(GlitchCoroutine(glitchDuration));
    }

    public void TriggerGlitch(float customDuration)
    {
        if (glitchRoutine != null) StopCoroutine(glitchRoutine);
        glitchRoutine = StartCoroutine(GlitchCoroutine(customDuration));
    }

    private IEnumerator GlitchCoroutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            currentGlitch = glitchCurve.Evaluate(t / duration);
            yield return null;
        }
        currentGlitch = 0f;
    }
}

// ------------------------------------------------------------------
// Exemplo de uso no script do monstro:
//
// public class MonsterAttack : MonoBehaviour
// {
//     private void OnTriggerEnter(Collider other)
//     {
//         if (other.CompareTag("Player"))
//             VHSGlitchController.Instance.TriggerGlitch();
//     }
// }
// ------------------------------------------------------------------
