using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    /// <summary>
    /// Portão de correr que fecha a passagem entre uma estação e a próxima. O diretor abre quando a
    /// lição termina. É um bloco que sobe para dentro do teto, com uma luz que muda de vermelho para
    /// verde - nada de rede: o tutorial é só do host.
    /// </summary>
    public class TutorialGate : MonoBehaviour
    {
        [SerializeField] private string gateId;
        [SerializeField] private Transform shutter;
        [SerializeField] private Renderer lamp;
        [SerializeField, Min(0.1f)] private float travel = 2.9f;
        [SerializeField, Min(0.05f)] private float seconds = 1.4f;
        [SerializeField] private bool startOpen;
        [SerializeField] private Color closedColor = new(0.9f, 0.15f, 0.1f);
        [SerializeField] private Color openColor = new(0.2f, 0.9f, 0.3f);

        private static readonly Dictionary<string, TutorialGate> ById = new();
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public string Id => gateId;
        public bool IsOpen { get; private set; }

        private Vector3 _closedLocal;
        private Coroutine _routine;

        public void Configure(string id, Transform shutterTransform, Renderer lampRenderer, bool open)
        {
            gateId = id;
            shutter = shutterTransform;
            lamp = lampRenderer;
            startOpen = open;
        }

        private void Awake()
        {
            if (shutter != null) _closedLocal = shutter.localPosition;
            IsOpen = startOpen;
            Apply(IsOpen ? 1f : 0f);
            Paint(IsOpen);
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(gateId)) ById[gateId] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(gateId) && ById.TryGetValue(gateId, out TutorialGate gate) && gate == this) ById.Remove(gateId);
        }

        public static TutorialGate Find(string id) => ById.TryGetValue(id, out TutorialGate gate) ? gate : null;

        public static void Open(string id) => Find(id)?.Open();

        public static void Close(string id) => Find(id)?.Close();

        public static void Set(string id, bool open)
        {
            if (open) Open(id);
            else Close(id);
        }

        public void Open() => Drive(true);

        public void Close() => Drive(false);

        private void Drive(bool open)
        {
            if (IsOpen == open) return;

            IsOpen = open;
            Paint(open);

            if (_routine != null) StopCoroutine(_routine);
            if (!isActiveAndEnabled)
            {
                Apply(open ? 1f : 0f);
                return;
            }

            _routine = StartCoroutine(Animate(open));
        }

        private IEnumerator Animate(bool open)
        {
            float from = CurrentAmount();
            float to = open ? 1f : 0f;
            float timer = 0f;
            float duration = Mathf.Max(0.05f, seconds * Mathf.Abs(to - from));

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));
                Apply(Mathf.Lerp(from, to, t));
                yield return null;
            }

            Apply(to);
            _routine = null;
        }

        private float CurrentAmount()
        {
            if (shutter == null || travel <= 0f) return IsOpen ? 1f : 0f;
            return Mathf.Clamp01((shutter.localPosition.y - _closedLocal.y) / travel);
        }

        private void Apply(float amount)
        {
            if (shutter == null) return;
            shutter.localPosition = _closedLocal + Vector3.up * (travel * amount);
        }

        private void Paint(bool open)
        {
            if (lamp == null) return;

            Color color = open ? openColor : closedColor;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            lamp.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(EmissionColor, color * 2f);
            lamp.SetPropertyBlock(block);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ById.Clear();
    }
}
