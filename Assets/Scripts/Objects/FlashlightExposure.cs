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
    /// distance ^ (compensation - 2) instead. Closer than the reference it is undone hard, so walls
    /// stop glaring; farther it is undone gently, because every change of intensity shows as the
    /// beam pumping when it is swept across a room.</para>
    ///
    /// <para>The distance is the nearest hit among a ray down the middle of the beam and two rings
    /// around it, so a door frame or a shelf edge in the beam dims it too, not only whatever sits
    /// dead centre. The outer ring lands on the spill, which the cookie keeps at about a quarter of
    /// the hotspot, so a hit there glares like a hotspot twice as far away and counts that much
    /// farther.</para>
    ///
    /// <para>The spill only follows part of the dimming and never the boost: it lights the room
    /// around the beam, and scaling it with the beam made the whole room darken whenever the beam
    /// passed over something close.</para>
    ///
    /// <para>Closer than the close distance the falloff is undone completely, for the beam and the
    /// spill alike, so what is right in front of the lens keeps the brightness it had there instead
    /// of turning white. The lens sits about ten centimetres from a wall the player walks into.</para>
    /// </remarks>
    public class FlashlightExposure : MonoBehaviour
    {
        private const int RingRays = 6;

        [SerializeField] private Light beam;

        [Tooltip("Optional. The wide fill light switched with the beam.")]
        [SerializeField] private Light spill;

        [Tooltip("Beam intensity when what it points at is at the reference distance.")]
        [SerializeField, Min(0f)] private float beamIntensity = 70f;

        [Tooltip("Spill intensity when nothing is close to the beam.")]
        [SerializeField, Min(0f)] private float spillIntensity = 5f;

        [Tooltip("Metres at which the beam shines at exactly the intensity above.")]
        [SerializeField, Min(0.1f)] private float referenceDistance = 3f;

        [Tooltip("How much of the distance falloff is undone closer than the reference. 0 = a plain light, 2 = the hotspot looks the same at any distance.")]
        [SerializeField, Range(0f, 2f)] private float nearCompensation = 1.5f;

        [Tooltip("The same, farther than the reference. Keep it low: the higher, the more the beam pumps when swept across a room.")]
        [SerializeField, Range(0f, 2f)] private float farCompensation = 1f;

        [Tooltip("Metres below which what the beam points at gets no brighter. Keep it below the reference distance.")]
        [SerializeField, Min(0.01f)] private float closeDistance = 1f;

        [Tooltip("Lowest multiplier, for a wall touching the lens.")]
        [SerializeField, Min(0.0001f)] private float minScale = 0.002f;

        [Tooltip("Highest multiplier, for a long corridor. Too high and the walls beside the player glow.")]
        [SerializeField, Min(0.01f)] private float maxScale = 4f;

        [Tooltip("How fast the beam dims when something comes close. Too slow and a wall flashes white first.")]
        [SerializeField, Min(0f)] private float dimSpeed = 4f;

        [Tooltip("How fast it comes back up when what it points at moves away.")]
        [SerializeField, Min(0f)] private float brightenSpeed = 1.5f;

        [Tooltip("How much of the beam's dimming near walls the spill follows. 0 = always full, 1 = dims as much as the beam.")]
        [SerializeField, Range(0f, 1f)] private float spillDimming = 0.6f;

        [Tooltip("Width of the inner probe ring, as a fraction of the beam's half-angle. Keep it on the hotspot's edge.")]
        [SerializeField, Range(0f, 1f)] private float innerRing = 0.2f;

        [Tooltip("Width of the outer probe ring, as a fraction of the beam's half-angle. Lands on the spill.")]
        [SerializeField, Range(0f, 1f)] private float outerRing = 0.6f;

        [Tooltip("How many times farther a hit on the outer ring counts. About 1 / sqrt(spill brightness).")]
        [SerializeField, Min(1f)] private float outerRingWeight = 2f;

        [Tooltip("What the probes stop at. Empty = everything but the players and their ragdolls.")]
        [SerializeField] private LayerMask probeLayers;

        private float _logScale;
        private float _logClose;
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
            float distance = ProbeDistance();
            float target = Mathf.Log(TargetScale(distance));
            float closeTarget = Mathf.Log(Mathf.Min(distance, closeDistance) / closeDistance);

            if (_wasOn)
            {
                _logScale = Ease(_logScale, target);
                _logClose = Ease(_logClose, closeTarget);
            }
            else
            {
                // Just switched on: no fade from wherever it was pointed last time.
                _logScale = target;
                _logClose = closeTarget;
                _wasOn = true;
            }

            float scale = Mathf.Exp(_logScale);
            float close = Mathf.Exp(_logClose);

            // The scale leaves the hotspot falling off as distance ^ (nearCompensation - 2) and the
            // spill as distance ^ -2: undo what is left of that closer than the close distance.
            beam.intensity = beamIntensity * Mathf.Max(scale * Mathf.Pow(close, 2f - nearCompensation), minScale);

            if (spill != null)
            {
                spill.intensity = spillIntensity * Mathf.Lerp(1f, Mathf.Min(scale, 1f), spillDimming) * close * close;
            }
        }

        private float Ease(float current, float target)
        {
            float speed = target < current ? dimSpeed : brightenSpeed;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        private float TargetScale(float distance)
        {
            float ratio = distance / referenceDistance;
            float scale = Mathf.Pow(ratio, ratio < 1f ? nearCompensation : farCompensation);

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
