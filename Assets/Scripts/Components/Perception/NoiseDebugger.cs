using UnityEngine;

namespace Components.Perception
{
    /// <summary>
    /// Tuning aid for the hearing system. Drop it anywhere in the scene while balancing noise
    /// ranges, and delete or disable it before shipping.
    ///
    /// <para>It owns <see cref="NoiseBus.IsVerboseLogging"/> so that flag has exactly one place
    /// it can be turned on, and it draws what a <see cref="HearingSensor"/> actually heard —
    /// which is the fastest way to feel whether the position error is too forgiving or too
    /// cruel.</para>
    /// </summary>
    public class NoiseDebugger : MonoBehaviour
    {
        [Tooltip("Log every noise reported to the bus, whether or not anything hears it.")]
        [SerializeField] private bool logEveryNoise;

        [Header("Gizmos")]
        [Tooltip("The sensor to visualise. Leave empty to find the first one in the scene.")]
        [SerializeField] private HearingSensor sensor;

        [SerializeField] private bool drawHearingRange = true;
        [SerializeField] private bool drawHeardNoises = true;

        private void OnEnable() => NoiseBus.IsVerboseLogging = logEveryNoise;

        private void OnDisable() => NoiseBus.IsVerboseLogging = false;

        private void OnValidate()
        {
            if (Application.isPlaying) NoiseBus.IsVerboseLogging = logEveryNoise;
        }

        private HearingSensor ResolveSensor()
        {
            if (sensor != null) return sensor;

            sensor = FindFirstObjectByType<HearingSensor>(FindObjectsInactive.Include);
            return sensor;
        }

        private void OnDrawGizmos()
        {
            HearingSensor target = ResolveSensor();
            if (target == null) return;

            if (drawHearingRange)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
                Gizmos.DrawWireSphere(target.EarPosition, target.MaxHearingRange);
            }

            if (!drawHeardNoises) return;

            foreach (HeardNoise heard in target.RecentlyHeard)
            {
                // Yellow = barely made it out, red = came through clearly.
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, heard.Confidence);

                Gizmos.DrawSphere(heard.TruePosition, 0.25f);
                Gizmos.DrawWireSphere(heard.Position, 0.4f);

                // The gap between the two is the monster's error — the whole design in one line.
                Gizmos.DrawLine(heard.TruePosition, heard.Position);
            }
        }
    }
}
