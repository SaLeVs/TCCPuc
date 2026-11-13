using System.Collections.Generic;
using Objects;
using UnityEngine;

namespace Tutorial
{
    /// <summary>
    /// A passagem de uma lição para a próxima: uma porta de verdade do jogo, trancada, com a luz de
    /// emergência em cima. Vermelha enquanto a lição não acabou; verde quando acaba, e a porta abre
    /// sozinha para o lado de lá.
    ///
    /// <para>Usa só a API pública da <see cref="Door"/>: <c>CloseAndLock</c> para segurar,
    /// <c>ClearLock</c> + <c>ForceOpenFrom</c> para abrir (o mesmo caminho do monstro quando força uma
    /// porta, que segura ela aberta uns segundos). Tudo no servidor - o tutorial é só do host.</para>
    /// </summary>
    public class TutorialDoorGate : MonoBehaviour
    {
        private const float LockSeconds = 1000000f;

        [SerializeField] private string gateId;
        [SerializeField] private Door door;

        [Tooltip("Um ponto do lado de quem chega. A porta abre para longe dele.")]
        [SerializeField] private Transform approach;

        [SerializeField] private Light[] lamps;
        [SerializeField] private Renderer[] lampRenderers;
        [SerializeField] private Color closedColor = new(1f, 0.06f, 0.04f);
        [SerializeField] private Color openColor = new(0.1f, 1f, 0.25f);

        private static readonly Dictionary<string, TutorialDoorGate> ById = new();
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public string Id => gateId;
        public Door Door => door;
        public bool IsOpen { get; private set; }

        public void Configure(string id, Door gateDoor, Transform approachPoint, Light[] gateLamps, Renderer[] gateRenderers)
        {
            gateId = id;
            door = gateDoor;
            approach = approachPoint;
            lamps = gateLamps;
            lampRenderers = gateRenderers;
        }

        private void Awake() => Paint(false);

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(gateId)) ById[gateId] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(gateId) && ById.TryGetValue(gateId, out TutorialDoorGate gate) && gate == this) ById.Remove(gateId);
        }

        public static TutorialDoorGate Find(string id) => ById.TryGetValue(id, out TutorialDoorGate gate) ? gate : null;

        public static void Open(string id) => Find(id)?.Open();

        /// <summary>Tranca todas as portas que ainda não abriram. Chamado quando o host já subiu.</summary>
        public static void LockAll()
        {
            foreach (TutorialDoorGate gate in ById.Values)
            {
                if (!gate.IsOpen) gate.Lock();
            }
        }

        /// <summary>A porta em <paramref name="position"/> é de um portão ainda fechado (o diretor explica no chat).</summary>
        public static bool IsClosedGateAt(Vector3 position)
        {
            foreach (TutorialDoorGate gate in ById.Values)
            {
                if (gate.IsOpen || gate.door == null) continue;
                if (Vector3.Distance(gate.door.transform.position, position) < 1.5f) return true;
            }

            return false;
        }

        public void Lock()
        {
            if (door != null && door.IsSpawned) door.CloseAndLock(LockSeconds);
        }

        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            Paint(true);

            if (door == null || !door.IsSpawned) return;

            door.ClearLock();
            door.ForceOpenFrom(approach != null ? approach.position : transform.position - transform.forward);
        }

        private void Paint(bool open)
        {
            Color color = open ? openColor : closedColor;

            if (lamps != null)
            {
                foreach (Light lamp in lamps)
                {
                    if (lamp != null) lamp.color = color;
                }
            }

            if (lampRenderers == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();

            foreach (Renderer lamp in lampRenderers)
            {
                if (lamp == null) continue;

                lamp.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(EmissionColor, color * 3f);
                lamp.SetPropertyBlock(block);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ById.Clear();
    }
}
