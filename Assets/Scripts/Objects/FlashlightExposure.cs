using UnityEngine;

namespace Objects
{
    /// <summary>
    /// Sets the flashlight's intensity from how far away what it points at is, so a wall right in
    /// front of the player is not blown out and the far end of a room still gets light. Purely
    /// visual: every peer runs it on every flashlight it can see.
    /// </summary>
    /// <remarks>
    /// <para>URP lights fall off with the square of the distance: a beam bright enough at ten
    /// metres is four hundred times brighter at half a metre. Multiplying the intensity by
    /// (distance / reference) ^ compensation leaves the hotspot falling off as
    /// distance ^ (compensation - 2) instead - at 1.5, that gap shrinks to about four times.</para>
    ///
    /// <para>The distance is the nearest hit among a ray down the middle of the beam and two rings
    /// around it, so a door frame or a shelf edge in the beam dims it too, not only whatever sits
    /// dead centre. The outer ring lands on the spill, which the cookie keeps at about a quarter of
    /// the hotspot, so a hit there glares like a hotspot twice as far away and counts that much
    /// farther.</para>
    /// </remarks>
    public class FlashlightExposure : MonoBehaviour
    {
        private const int RingRays = 6;

        [SerializeField] private Light beam;

        [Tooltip("Optional. The wide fill light switched with the beam; scaled the same way.")]
        [SerializeField] private Light spill;

        [Tooltip("Beam intensity when what it points at is at the reference distance.")]
        [SerializeField, Min(0f)] private float beamIntensity = 70f;

        [Tooltip("Spill intensity when what the beam points at is at the reference distance.")]
        [SerializeField, Min(0f)] private float spillIntensity = 5f;

        [Tooltip("Metres at which both lights shine at exactly the intensities above.")]
        [SerializeField, Min(0.1f)] private float referenceDistance = 3f;

        [Tooltip("How much of the distance falloff is undone. 0 = a plain light, 2 = the hotspot looks the same at any distance.")]
        [SerializeField, Range(0f, 2f)] private float compensation = 1.5f;

        [Tooltip("Lowest multiplier, for a wall touching the lens.")]
        [SerializeField, Min(0.01f)] private float minScale = 0.04f;

        [Tooltip("Highest multiplier, for a long corridor. Too high and the walls beside the player glow.")]
        [SerializeField, Min(0.01f)] private float maxScale = 8f;

        [Tooltip("How fast the beam dims when something comes close. Too slow and a wall flashes white first.")]
        [SerializeField, Min(0f)] private float dimSpeed = 12f;

        [Tooltip("How fast it comes back up when what it points at moves away.")]
        [SerializeField, Min(0f)] private float brightenSpeed = 4f;

        [Tooltip("Width of the inner probe ring, as a fraction of the beam's half-angle. Keep it on the hotspot's edge.")]
        [SerializeField, Range(0f, 1f)] private float innerRing = 0.2f;

        [Tooltip("Width of the outer probe ring, as a fraction of the beam's half-angle. Lands on the spill.")]
        [SerializeField, Range(0f, 1f)] private float outerRing = 0.6f;

        [Tooltip("How many times farther a hit on the outer ring counts. About 1 / sqrt(spill brightness).")]
        [SerializeField, Min(1f)] private float outerRingWeight = 2f;

        [Tooltip("What the probes stop at. Empty = everything but the players and their ragdolls.")]
        [SerializeField] private LayerMask probeLayers;

        private float _logScale;
        private bool _wasOn;

        private void Awake()
        {
            if (probeLayers.value == 0)
            {
                probeLayers = ~LayerMask.GetMask("Player", "DeadPlayer", "Body", "OcclusionBody",
                    "Ignore Raycast", "UI", "TransparentFX");
            }
        }

        private void LateUpdate()
        {
            if (!beam.enabled)
            {
                _wasOn = false;
                return;
            }

            // Eased in log space, so going from 0.1x to 1x takes as long as from 1x to 10x.
            float target = Mathf.Log(TargetScale(ProbeDistance()));

            if (_wasOn)
            {
                float speed = target < _logScale ? dimSpeed : brightenSpeed;
                _logScale = Mathf.Lerp(_logScale, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
            }
            else
            {
                // Just switched on: no fade from wherever it was pointed last time.
                _logScale = target;
                _wasOn = true;
            }

            float scale = Mathf.Exp(_logScale);
            beam.intensity = beamIntensity * scale;

            if (spill != null) spill.intensity = spillIntensity * scale;
        }

        private float TargetScale(float distance)
        {
            float scale = Mathf.Pow(distance / referenceDistance, compensation);

            return Mathf.Clamp(scale, minScale, Mathf.Max(minScale, maxScale));
        }

        private float ProbeDistance()
        {
            Transform t = beam.transform;
            Vector3 origin = t.position;
            Vector3 forward = t.forward;
            float reach = beam.range;

            float nearest = Probe(origin, forward, reach);
            nearest = Mathf.Min(nearest, ProbeRing(t, innerRing, reach));
            nearest = Mathf.Min(nearest, ProbeRing(t, outerRing, reach) * outerRingWeight);

            return nearest;
        }

        private float ProbeRing(Transform t, float spread, float reach)
        {
            Vector3 forward = t.forward;
            Quaternion tilt = Quaternion.AngleAxis(beam.spotAngle * 0.5f * spread, t.right);
            float nearest = reach;

            for (int i = 0; i < RingRays; i++)
            {
                Quaternion spin = Quaternion.AngleAxis(i * (360f / RingRays), forward);
                nearest = Mathf.Min(nearest, Probe(t.position, spin * (tilt * forward), reach));
            }

            return nearest;
        }

        private float Probe(Vector3 origin, Vector3 direction, float reach)
        {
            bool hit = Physics.Raycast(origin, direction, out RaycastHit info, reach, probeLayers,
                QueryTriggerInteraction.Ignore);

            return hit ? info.distance : reach;
        }
    }
}
