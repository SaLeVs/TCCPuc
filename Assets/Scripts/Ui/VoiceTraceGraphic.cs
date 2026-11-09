using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// The voice drawn as ink across the mic setup sheet, newest on the right: a strip of bars that
    /// grow with the level, mirrored around a centre line like a recorder's trace, older ones fading.
    /// It shows the same AudioEnergy as everything else on the sheet; Vivox only reads it five times a
    /// second, so whoever feeds it glides between readings and the bars carry a little grain.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoiceTraceGraphic : MaskableGraphic
    {
        [SerializeField, Min(8)] private int columns = 90;
        [SerializeField, Min(0.5f)] private float secondsShown = 5f;

        [Tooltip("Fraction of each column the bar takes; the rest is the gap.")]
        [SerializeField, Range(0.1f, 1f)] private float barWidth = 0.55f;

        [SerializeField, Min(0f)] private float minBarHeight = 3f;
        [SerializeField, Min(0f)] private float centerLineThickness = 2f;

        [Tooltip("Opacity of the oldest bar; the newest is fully opaque.")]
        [SerializeField, Range(0f, 1f)] private float oldestAlpha = 0.15f;

        [Tooltip("How much each bar varies around the level, so a steady voice still reads as a voice.")]
        [SerializeField, Range(0f, 0.5f)] private float grain = 0.18f;

        [Tooltip("Bar height = level ^ this. Below 1 a whisper still draws a visible bar; louder is " +
                 "always taller.")]
        [SerializeField, Range(0.2f, 1f)] private float response = 0.5f;

        private float[] _values;
        private int _head;
        private float _elapsed;
        private float _level;


        /// <summary>The level the next bars are drawn at, 0..1 of the strip's half height.</summary>
        public void SetLevel(float level) => _level = Mathf.Pow(Mathf.Clamp01(level), response);

        public void Clear()
        {
            if (_values != null) System.Array.Clear(_values, 0, _values.Length);
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            _values = new float[columns];
        }

        private void Update()
        {
            if (_values == null || _values.Length != columns) _values = new float[columns];

            float columnSeconds = secondsShown / columns;
            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed < columnSeconds) return;

            while (_elapsed >= columnSeconds)
            {
                _elapsed -= columnSeconds;

                float variation = 1f + grain * (Mathf.PerlinNoise(Time.unscaledTime * 7f, _head * 0.37f) * 2f - 1f);
                _values[_head] = Mathf.Clamp01(_level * variation);
                _head = (_head + 1) % _values.Length;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            float centerY = rect.center.y;

            AddQuad(vh, rect.xMin, centerY - centerLineThickness * 0.5f, rect.xMax, centerY + centerLineThickness * 0.5f,
                WithAlpha(oldestAlpha + (1f - oldestAlpha) * 0.5f));

            if (_values == null || _values.Length == 0) return;

            float step = rect.width / _values.Length;
            float halfBar = step * barWidth * 0.5f;

            for (int i = 0; i < _values.Length; i++)
            {
                // Oldest first, so the newest bar is drawn at the right edge.
                float value = _values[(_head + i) % _values.Length];
                float half = Mathf.Max(minBarHeight * 0.5f, value * rect.height * 0.5f);
                float x = rect.xMin + (i + 0.5f) * step;
                float age = i / (_values.Length - 1f);

                AddQuad(vh, x - halfBar, centerY - half, x + halfBar, centerY + half,
                    WithAlpha(Mathf.Lerp(oldestAlpha, 1f, age)));
            }
        }

        private Color32 WithAlpha(float alpha)
        {
            Color ink = color;
            ink.a *= alpha;
            return ink;
        }

        private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color32 tint)
        {
            int start = vh.currentVertCount;

            vh.AddVert(new Vector3(xMin, yMin), tint, Vector2.zero);
            vh.AddVert(new Vector3(xMin, yMax), tint, Vector2.up);
            vh.AddVert(new Vector3(xMax, yMax), tint, Vector2.one);
            vh.AddVert(new Vector3(xMax, yMin), tint, Vector2.right);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
