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
    ///
    /// <para>The markers answer one question: <i>did the monster hear that?</i> A solid blob
    /// appears exactly where a noise it heard was made. No blob means the noise never got
    /// through — too quiet, too far, or too many walls — and the sound is the thing to fix, not
    /// the AI. Where it decides to walk is drawn by <c>MonsterAwareness</c> instead.</para>
    ///
    /// <para>Server-side only: <see cref="HearingSensor"/> subscribes to the bus only on the
    /// server, so run as Host to see anything. A pure LAN client draws nothing, and that is
    /// expected rather than a bug.</para>
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

        [Header("Heard noise marker")]
        [Tooltip("Solid blob dropped exactly where a noise the monster heard was made.")]
        [SerializeField] private Color heardNoiseColor = new Color(1f, 0f, 0.9f);

        [SerializeField, Min(0.05f)] private float heardNoiseRadius = 0.55f;

        [Tooltip("Seconds a marker stays up. It fades over this time so the noise you just made " +
                 "is obvious next to the ones before it. 0 keeps every marker the sensor still holds.")]
        [SerializeField, Min(0f)] private float heardNoiseLifetime = 5f;

        [Tooltip("Also draw the monster's guess — the scattered point it will actually walk to — " +
                 "and the error between guess and truth. The gap is the whole design of the hearing system.")]
        [SerializeField] private bool drawGuessedPosition = true;

        [Tooltip("Label each marker with its type and how clearly it came through (0..1).")]
        [SerializeField] private bool drawLabels = true;

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
                float fade = FadeFor(heard);
                if (fade <= 0f) continue;

                DrawHeardNoise(heard, fade);
            }
        }

        /// <summary>
        /// 1 the frame a noise lands, falling to 0 over <c>heardNoiseLifetime</c>. Drives alpha so
        /// a marker announces itself and then gets out of the way, instead of the buffer's last
        /// twelve entries all sitting there at full strength with no way to tell them apart.
        /// </summary>
        private float FadeFor(HeardNoise heard)
        {
            if (heardNoiseLifetime <= 0f) return 1f;

            float age = Time.time - heard.HeardAtTime;

            return Mathf.Clamp01(1f - age / heardNoiseLifetime);
        }

        private void DrawHeardNoise(HeardNoise heard, float fade)
        {
            Color color = heardNoiseColor;
            color.a *= fade;

            Gizmos.color = color;
            Gizmos.DrawSphere(heard.TruePosition, heardNoiseRadius);

            if (drawGuessedPosition)
            {
                // Hollow, so the solid blob stays the thing your eye lands on: that one is the
                // truth, this one is only where the monster thinks it came from.
                Gizmos.DrawWireSphere(heard.Position, heardNoiseRadius * 1.6f);
                Gizmos.DrawLine(heard.TruePosition, heard.Position);
            }

            if (!drawLabels) return;

            DrawLabel(heard.TruePosition + Vector3.up * (heardNoiseRadius + 0.35f),
                $"{heard.Type} {heard.Confidence:0.00}", color);
        }

        private static void DrawLabel(Vector3 position, string text, Color color)
        {
#if UNITY_EDITOR
            _labelStyle ??= new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _labelStyle.normal.textColor = color;

            UnityEditor.Handles.Label(position, text, _labelStyle);
#endif
        }

#if UNITY_EDITOR
        private static GUIStyle _labelStyle;
#endif
    }
}
