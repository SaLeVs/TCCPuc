using Components.Perception;
using UnityEngine;

namespace Monster
{
    /// <summary>
    /// A plain MonoBehaviour, not a NetworkBehaviour: nothing here is replicated. The meter is
    /// written only by the server (the brain gates every call behind IsServer) and read only by
    /// the state machine, which also runs server-side. Registering it with NGO bought nothing
    /// and left every client carrying a copy permanently stuck at zero — a trap for the first
    /// UI or client-side effect that tried to read the suspicion level.
    /// </summary>
    public class MonsterAwareness : MonoBehaviour
    {
        [Tooltip("Awareness a fully-clear noise adds. Fainter noises add proportionally less, so one distant footstep is never enough on its own")]
        [SerializeField, Range(0f, 1f)] private float awarenessPerNoise = 0.45f;

        [Tooltip("Awareness lost per second while nothing is heard.")]
        [SerializeField, Min(0f)] private float decayPerSecond = 0.12f;

        [Tooltip("Above this, the monster walks over to look (Investigate).")]
        [SerializeField, Range(0f, 1f)] private float suspiciousThreshold = 0.25f;

        [Tooltip("Above this, the monster moves fast to the spot and sweeps it (Search).")]
        [SerializeField, Range(0f, 1f)] private float alertedThreshold = 0.6f;

        [Header("How alarming each kind of noise is")]
        [Tooltip("Scales the suspicion a noise of this type adds. The sensor already decides " +
                 "whether a noise was heard and how clearly; this decides how much it matters. " +
                 "A type missing from the list counts as 1.")]
        [SerializeField]
        private NoiseWeight[] noiseWeights =
        {
            new NoiseWeight { type = NoiseType.Footstep,    multiplier = 1.0f },
            new NoiseWeight { type = NoiseType.Voice,       multiplier = 1.4f },
            new NoiseWeight { type = NoiseType.Flashlight,  multiplier = 0.7f },
            new NoiseWeight { type = NoiseType.Interaction, multiplier = 1.0f },
            new NoiseWeight { type = NoiseType.Impact,      multiplier = 1.6f },
            new NoiseWeight { type = NoiseType.Door,        multiplier = 1.3f },
        };

        [Header("Debug gizmos")]
        [Tooltip("Draw the spot the monster is on its way to check, for as long as it is actually " +
                 "committed to checking it. Pairs with the solid markers NoiseDebugger drops where " +
                 "a noise was heard: a marker with no target means it heard you but stayed put.")]
        [SerializeField] private bool drawInvestigationGizmo = true;

        [Tooltip("Suspicious - walking over to look (InvestigateState).")]
        [SerializeField] private Color investigateColor = new Color(0.15f, 1f, 0.35f);

        [Tooltip("Alerted - moving fast to the spot and sweeping it (SearchState).")]
        [SerializeField] private Color searchColor = new Color(1f, 0.45f, 0f);

        [SerializeField, Min(0.05f)] private float investigationGizmoSize = 0.8f;

        [Tooltip("Height of the beam standing on the target, so you can find it from across the " +
                 "map and through geometry.")]
        [SerializeField, Min(0f)] private float investigationBeamHeight = 4f;

        [System.Serializable]
        private struct NoiseWeight
        {
            public NoiseType type;

            [Min(0f)] public float multiplier;
        }
        
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
            // Confidence alone treats every noise the same up close: a crouched footstep and a
            // scream both arrive at 1.0, so loudness only ever controlled range, never how
            // alarming the thing was. The per-type weight is what makes shouting a real risk.
            float weight = WeightFor(heard.Type);

            _awareness = Mathf.Clamp01(_awareness + heard.Confidence * awarenessPerNoise * weight);

            InvestigationPoint = heard.Position;
            LastHeardNoiseType = heard.Type;
        }

        private float WeightFor(NoiseType type)
        {
            // Linear scan: the enum has a handful of values, so a dictionary would cost more in
            // setup than it saves per lookup.
            for (int i = 0; i < noiseWeights.Length; i++)
            {
                if (noiseWeights[i].type == type) return noiseWeights[i].multiplier;
            }

            return 1f;
        }
        
        public void RegisterLostSight(Vector3 lastKnownPosition)
        {
            _awareness = 1f;
            InvestigationPoint = lastKnownPosition;
        }
        
        public void PinToMax() => _awareness = 1f;

        /// <summary>
        /// While held, the meter stops decaying.
        ///
        /// <para>Decay is there for the wandering case — a faint noise that was not worth acting
        /// on fades away. It was never meant to cancel an investigation already under way, but
        /// that is what it did: one footstep bought 1.67 s of suspicion, and walking to the noise
        /// takes longer than that, so the monster gave up before arriving. <see cref="MonsterStates.ParentStates.MonsterAlert"/>
        /// holds the meter for as long as it is committed to going and looking, and the Alert
        /// states decide for themselves when they are done.</para>
        /// </summary>
        public bool IsHeld { get; private set; }

        public void Hold() => IsHeld = true;

        public void Release() => IsHeld = false;

        public void Tick(float deltaTime)
        {
            if (IsHeld) return;
            if (_awareness <= 0f) return;

            _awareness = Mathf.MoveTowards(_awareness, 0f, decayPerSecond * deltaTime);
        }
        
        public void Clear()
        {
            _awareness = Mathf.Min(_awareness, suspiciousThreshold * 0.5f);
        }


        /// <summary>
        /// Draws where the monster is headed and how seriously it is taking it.
        ///
        /// <para>Gated on <see cref="IsHeld"/> rather than on <see cref="ShouldInvestigate"/>,
        /// because only <see cref="MonsterStates.ParentStates.MonsterAlert"/> sets it — on the way
        /// in, cleared on the way out. That makes it exactly "committed to going and looking".
        /// The level is already over the threshold for the frame or two before the transition
        /// actually runs, so drawing on that would flash a target the monster never went to.</para>
        ///
        /// <para>Server-only, like everything else here: the state machine runs nowhere else, so
        /// a LAN client draws nothing. Run as Host.</para>
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!drawInvestigationGizmo) return;
            if (!Application.isPlaying) return;
            if (!IsHeld) return;

            bool isAlerted = Level == AwarenessLevel.Alerted;
            Color color = isAlerted ? searchColor : investigateColor;

            Vector3 point = InvestigationPoint;

            Gizmos.color = color;

            // A cube, where a heard noise is a sphere: the two are told apart by shape first, so
            // they still read correctly when they overlap or when the colours get retuned.
            Gizmos.DrawCube(point, Vector3.one * investigationGizmoSize);
            Gizmos.DrawLine(point, point + Vector3.up * investigationBeamHeight);
            Gizmos.DrawLine(transform.position, point);

            Gizmos.color = new Color(color.r, color.g, color.b, color.a * 0.3f);
            Gizmos.DrawWireSphere(point, investigationGizmoSize * 2f);

            DrawLabel(point + Vector3.up * (investigationBeamHeight + 0.3f),
                $"{(isAlerted ? "Search" : "Investigate")} | {LastHeardNoiseType} | {_awareness:0.00}",
                color);
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
