using Components.Perception;
using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterAwareness : NetworkBehaviour
    {
        [Tooltip("Awareness a fully-clear noise adds. Fainter noises add proportionally less, so one distant footstep is never enough on its own")]
        [SerializeField, Range(0f, 1f)] private float awarenessPerNoise = 0.45f;

        [Tooltip("Awareness lost per second while nothing is heard.")]
        [SerializeField, Min(0f)] private float decayPerSecond = 0.12f;

        [Tooltip("Above this, the monster walks over to look (Investigate).")]
        [SerializeField, Range(0f, 1f)] private float suspiciousThreshold = 0.25f;

        [Tooltip("Above this, the monster moves fast to the spot and sweeps it (Search).")]
        [SerializeField, Range(0f, 1f)] private float alertedThreshold = 0.6f;
        
        public float Value => _awareness;

        public AwarenessLevel Level => _awareness >= alertedThreshold ? AwarenessLevel.Alerted
            : _awareness >= suspiciousThreshold ? AwarenessLevel.Suspicious
            : AwarenessLevel.Unaware;
        
        public bool ShouldInvestigate => Level != AwarenessLevel.Unaware;


        public Vector3 InvestigationPoint { get; private set; }
        public NoiseType LastHeardNoiseType { get; private set; }

        private float _awareness;

        
        public void RegisterNoise(HeardNoise heard)
        {
            _awareness = Mathf.Clamp01(_awareness + heard.Confidence * awarenessPerNoise);
            
            InvestigationPoint = heard.Position;
            LastHeardNoiseType = heard.Type;
        }
        
        public void RegisterLostSight(Vector3 lastKnownPosition)
        {
            _awareness = 1f;
            InvestigationPoint = lastKnownPosition;
        }
        
        public void PinToMax() => _awareness = 1f;

        public void Tick(float deltaTime)
        {
            if (_awareness <= 0f) return;

            _awareness = Mathf.MoveTowards(_awareness, 0f, decayPerSecond * deltaTime);
        }
        
        public void Clear()
        {
            _awareness = Mathf.Min(_awareness, suspiciousThreshold * 0.5f);
        }
    }
}
