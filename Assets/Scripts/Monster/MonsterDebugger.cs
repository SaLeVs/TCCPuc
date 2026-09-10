using Components;
using Components.Perception;
using UnityEngine;

namespace Monster
{
    /// <summary>
    /// Read-out for what the monster is thinking. Put it on the monster while tuning and
    /// disable it before shipping.
    ///
    /// <para>Everything here runs server-side, because that is the only place the AI exists —
    /// on a client the state path is empty and the overlay says so rather than showing a
    /// misleading blank.</para>
    /// </summary>
    public class MonsterDebugger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MonsterBrain brain;
        [SerializeField] private MonsterAwareness awareness;
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private HearingSensor hearingSensor;

        [Header("Output")]
        [Tooltip("Log every state change to the console.")]
        [SerializeField] private bool logStateChanges = true;

        [Tooltip("Draw a live overlay in the top-left of the Game view.")]
        [SerializeField] private bool showOverlay = true;

        [Header("Gizmos")]
        [Tooltip("Draw a line to everyone currently in vision.")]
        [SerializeField] private bool drawVisionLinks = true;

        [Tooltip("Draw where the monster is currently headed while Alert.")]
        [SerializeField] private bool drawInvestigationPoint = true;

        private GUIStyle _style;

        private void Awake()
        {
            if (brain == null) brain = GetComponent<MonsterBrain>();
            if (awareness == null) awareness = GetComponent<MonsterAwareness>();
            if (visionSensor == null) visionSensor = GetComponent<VisionSensor>();
            if (hearingSensor == null) hearingSensor = GetComponent<HearingSensor>();
        }

        private void OnEnable()
        {
            if (brain != null) brain.OnStateChanged += Brain_OnStateChanged;
        }

        private void OnDisable()
        {
            if (brain != null) brain.OnStateChanged -= Brain_OnStateChanged;
        }

        private void Brain_OnStateChanged(string statePath)
        {
            if (!logStateChanges) return;

            Debug.Log($"Monster: State: {statePath}", this);
        }

        private void OnGUI()
        {
            if (!showOverlay || brain == null) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                richText = true
            };

            string state = string.IsNullOrEmpty(brain.CurrentStatePath)
                ? "<i>(no state — this peer is not the server)</i>"
                : brain.CurrentStatePath;

            string awarenessLine = awareness == null
                ? "-"
                : $"{awareness.Level}  {awareness.Value:0.00}  (last noise: {awareness.LastHeardNoiseType})";

            int seen = brain._playersInVision.Count;

            GUI.Box(new Rect(8, 8, 520, 78), GUIContent.none);
            GUI.Label(new Rect(16, 12, 500, 20), $"<b>State</b>  {state}", _style);
            GUI.Label(new Rect(16, 32, 500, 20), $"<b>Awareness</b>  {awarenessLine}", _style);
            GUI.Label(new Rect(16, 52, 500, 20), $"<b>In vision</b>  {seen}   <b>Tracking</b>  {brain.IsTrackingLostTarget}", _style);
        }

        private void OnDrawGizmos()
        {
            if (brain == null) return;

            if (drawVisionLinks)
            {
                Gizmos.color = Color.green;

                foreach (Transform seen in brain._playersInVision)
                {
                    if (seen == null) continue;

                    Gizmos.DrawLine(transform.position + Vector3.up, seen.position + Vector3.up);
                    Gizmos.DrawWireSphere(seen.position + Vector3.up, 0.35f);
                }
            }

            if (!drawInvestigationPoint || awareness == null) return;
            if (awareness.Level == AwarenessLevel.Unaware) return;

            // Blue while merely curious, magenta once it is committed to a sweep.
            Gizmos.color = awareness.Level == AwarenessLevel.Alerted ? Color.magenta : Color.cyan;
            Gizmos.DrawWireCube(awareness.InvestigationPoint + Vector3.up * 0.5f, Vector3.one * 0.6f);
            Gizmos.DrawLine(transform.position + Vector3.up, awareness.InvestigationPoint + Vector3.up * 0.5f);
        }
    }
}
