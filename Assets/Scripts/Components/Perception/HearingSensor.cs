using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Components.Perception
{
    public class HearingSensor : NetworkBehaviour
    {
        public event Action<HeardNoise> OnNoiseHeard;

        [Header("References")]
        [Tooltip("Where the monster hears from falls back to this transform")]
        [SerializeField] private Transform ear;

        [Header("Hearing")]
        [Tooltip("Multiplies every noise's loudness: 1 = noises are heard at their designed range")]
        [SerializeField, Min(0f)] private float sensitivity = 1f;

        [Tooltip("A noise must still have this many meters of loudness left at the ear to register")]
        [SerializeField, Min(0f)] private float audibilityFloor = 0.5f;

        [Tooltip("Hard cap. Nothing beyond this distance is ever heard, however loud")]
        [SerializeField, Min(0f)] private float maxHearingRange = 40f;

        [Header("Occlusion")]
        [Tooltip("Walls and geometry that muffle sound")]
        [SerializeField] private LayerMask occlusionLayers;

        [Tooltip("Meters of loudness lost per wall between the noise and the ear")]
        [SerializeField, Min(0f)] private float lossPerWall = 6f;

        [Tooltip("Stop counting walls after this many — keeps the raycast loop bounded")]
        [SerializeField, Min(1)] private int maxWallsCounted = 4;

        [Header("Accuracy")]
        [Tooltip("Largest position error, in meters, applied to a barely-audible noise, a noise at full confidence is located exactly")]
        [SerializeField, Min(0f)] private float maxPositionError = 6f;

        private readonly RaycastHit[] _occlusionHits = new RaycastHit[8];
        private readonly List<HeardNoise> _recentlyHeard = new();
        private const int RECENT_BUFFER_SIZE = 12;

        private Transform Ear => ear != null ? ear : transform;

        /// <summary>Last few noises that got through. Read by <see cref="NoiseDebugger"/>.</summary>
        public IReadOnlyList<HeardNoise> RecentlyHeard => _recentlyHeard;

        /// <summary>Where the monster hears from. Read by <see cref="NoiseDebugger"/>.</summary>
        public Vector3 EarPosition => Ear.position;

        public float MaxHearingRange => maxHearingRange;

        
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NoiseBus.OnNoise += NoiseBus_OnNoise;
        }
        

        private void NoiseBus_OnNoise(Noise noise)
        {
            if (!IsServer) return;
            if (!isActiveAndEnabled) return;
            
            if (noise.Source != null && noise.Source.IsChildOf(transform.root)) return;

            if (!TryHear(noise, out HeardNoise heard)) return;

            PushRecent(heard);
            OnNoiseHeard?.Invoke(heard);
        }
        
        public bool TryHear(Noise noise, out HeardNoise heard)
        {
            heard = default;

            Vector3 earPosition = Ear.position;
            Vector3 toNoise = noise.Position - earPosition;
            float distance = toNoise.magnitude;

            if (distance > maxHearingRange) return false;

            float reach = noise.Loudness * sensitivity;
            if (reach <= 0f) return false;
            
            float remaining = reach - distance;
            if (remaining <= audibilityFloor) return false;

            remaining -= CountWalls(earPosition, noise.Position, distance) * lossPerWall;
            if (remaining <= audibilityFloor) return false;
            
            float confidence = Mathf.Clamp01(remaining / reach);

            heard = new HeardNoise(Scatter(noise.Position, confidence), noise.Position, confidence, noise.Type, noise.Source);
            return true;
        }

        private int CountWalls(Vector3 from, Vector3 to, float distance)
        {
            if (distance <= 0.01f) return 0;

            Vector3 direction = (to - from) / distance;

            int hits = Physics.RaycastNonAlloc(from, direction, _occlusionHits, distance, occlusionLayers, QueryTriggerInteraction.Ignore);

            return Mathf.Min(hits, maxWallsCounted);
        }
        
        private Vector3 Scatter(Vector3 truePosition, float confidence)
        {
            float error = maxPositionError * (1f - confidence);

            if (error <= 0.05f) return truePosition;

            Vector2 offset = UnityEngine.Random.insideUnitCircle * error;
            Vector3 guess = truePosition + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(guess, out NavMeshHit navHit, Mathf.Max(error, 2f), NavMesh.AllAreas))
            {
                return navHit.position;
            }

            return truePosition;
        }

        private void PushRecent(HeardNoise heard)
        {
            _recentlyHeard.Add(heard);

            if (_recentlyHeard.Count > RECENT_BUFFER_SIZE)
            {
                _recentlyHeard.RemoveAt(0);
            }
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            NoiseBus.OnNoise -= NoiseBus_OnNoise;
        }
        
    }
}
