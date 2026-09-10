using UnityEngine;

namespace Components.Perception
{
    public class NoiseEmitter : MonoBehaviour
    {
        [SerializeField] private NoiseType type = NoiseType.Interaction;

        [Tooltip("How far this noise carries, in meters, with nothing in the way.")]
        [SerializeField, Min(0f)] private float loudness = 8f;

        [Tooltip("Ignore repeat calls that happen within this many seconds. 0 disables.")]
        [SerializeField, Min(0f)] private float cooldown = 0.1f;

        [Tooltip("Emit from this transform instead of the one this component sits on.")]
        [SerializeField] private Transform origin;

        private float _nextAllowedTime;

        public float Loudness
        {
            get => loudness;
            set => loudness = Mathf.Max(0f, value);
        }
        
        public void Emit() => Emit(1f);
        
        public void Emit(float loudnessScale)
        {
            if (cooldown > 0f && Time.time < _nextAllowedTime) return;
            _nextAllowedTime = Time.time + cooldown;

            Transform from = origin != null ? origin : transform;

            NoiseBus.Report(from.position, loudness * Mathf.Max(0f, loudnessScale), type, transform.root);
        }
    }
}
