using UnityEngine;

// Coloque este script NO MONSTRO (no objeto que tem o Collider).
// Dispara o glitch do VHS quando o monstro encosta no player.
public class VHSGlitchOnTouch : MonoBehaviour
{
    [Tooltip("Tag do player")]
    public string playerTag = "Player";

    [Tooltip("Tempo mínimo entre um glitch e outro (evita repetir sem parar)")]
    public float cooldown = 1f;

    private float nextTime;

    private void OnTriggerEnter(Collider other) { TryGlitch(other); }
    private void OnCollisionEnter(Collision collision) { TryGlitch(collision.collider); }

    private void TryGlitch(Collider other)
    {
        if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag)) return;
        if (Time.time < nextTime) return;

        nextTime = Time.time + cooldown;
        PlayGlitch();
    }

    // Pública de propósito: também pode ser chamada por um Animation Event
    // (no clipe de ataque) ou por um UnityEvent, se o ataque não for por colisão.
    public void PlayGlitch()
    {
        if (VHSGlitchController.Instance != null)
            VHSGlitchController.Instance.TriggerGlitch();
    }
}
