using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LevelDesign.Gym.VoiceLab
{
    /// <summary>
    /// The voice lab's controls and panel: puts you and the stand-in companion in a scenario of the
    /// map replica, lets you change how loud the companion talks, flip between the game's current
    /// voice curve and the one used before 2026-11-01, open doors, silence the rest of the audio to measure, and record
    /// your own voice for the companion to speak with. Reads the game; changes nothing in it.
    /// </summary>
    public class GymVoiceLab : MonoBehaviour
    {
        public enum Facing
        {
            [InspectorName("De frente para você")] TowardsPlayer,
            [InspectorName("De costas para você")] AwayFromPlayer,
            [InspectorName("Parado")] Fixed,
        }

        [Serializable]
        public class Scenario
        {
            public string name;
            [TextArea] public string hint;
            public Transform playerPoint;
            public Transform speakerPoint;
            public Facing facing = Facing.TowardsPlayer;
        }

        private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

        [SerializeField] private GymVoiceLabSpeaker speaker;
        [SerializeField] private Transform speakerBody;
        [SerializeField] private Scenario[] scenarios = Array.Empty<Scenario>();

        [Tooltip("Deslocamento da réplica em relação ao Game.unity, para mostrar as coordenadas do mapa original.")]
        [SerializeField] private Vector3 replicaOffset;

        [SerializeField] private bool showPanel = true;
        [SerializeField] private bool showRings = true;
        [SerializeField] private Facing facing = Facing.TowardsPlayer;
        [SerializeField, Min(1f)] private float recordSeconds = 6f;

        private int _scenario = -1;
        private bool _muteOthers;
        private float _nextMuteScan;
        private readonly List<AudioSource> _muted = new List<AudioSource>();

        private AudioClip _recording;
        private float _recordingUntil;

        private GymVoiceLabDoor[] _doors = Array.Empty<GymVoiceLabDoor>();
        private LineRenderer _rangeRing;
        private LineRenderer _whisperRing;
        private LineRenderer _speechRing;
        private LineRenderer _shoutRing;
        private LineRenderer _sightLine;
        private Material _lineMaterial;

        private string _toast;
        private float _toastUntil;
        private GUIStyle _box;
        private GUIStyle _text;

        private void Start()
        {
            _doors = FindObjectsByType<GymVoiceLabDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
            _rangeRing = MakeLine("Alcance agora", new Color(1f, 0.85f, 0.1f, 0.9f), 0.08f);
            _whisperRing = MakeLine("Sussurro", new Color(0.3f, 0.8f, 1f, 0.55f), 0.05f);
            _speechRing = MakeLine("Fala", new Color(0.35f, 1f, 0.45f, 0.55f), 0.05f);
            _shoutRing = MakeLine("Grito", new Color(1f, 0.3f, 0.3f, 0.55f), 0.05f);
            _sightLine = MakeLine("Linha até você", Color.green, 0.03f);
        }

        private void OnDestroy()
        {
            SetMuteOthers(false);
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        private void Update()
        {
            HandleKeys();
            UpdateFacing();
            UpdateRecording();
            UpdateMute();
            UpdateLines();
        }

        // ---------------------------------------------------------------- keys

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.gKey.wasPressedThisFrame) showPanel = !showPanel;
            if (keyboard.nKey.wasPressedThisFrame) NextScenario(shift ? -1 : 1);
            if (keyboard.pKey.wasPressedThisFrame) PlaceSpeakerAtPlayer();
            if (keyboard.hKey.wasPressedThisFrame) CycleFacing();
            if (keyboard.lKey.wasPressedThisFrame) showRings = !showRings;
            if (keyboard.jKey.wasPressedThisFrame) SetMuteOthers(!_muteOthers);

            if (speaker != null)
            {
                if (keyboard.yKey.wasPressedThisFrame) speaker.TogglePlaying();
                if (keyboard.uKey.wasPressedThisFrame) speaker.CycleLevel();
                if (keyboard.iKey.wasPressedThisFrame) speaker.ToggleCurve();
            }

            if (keyboard.oKey.wasPressedThisFrame)
            {
                if (shift) ToggleAllDoors();
                else ToggleNearestDoor();
            }

            if (keyboard.kKey.wasPressedThisFrame)
            {
                if (shift) UseGeneratedSpeech();
                else StartRecording();
            }
        }

        // ---------------------------------------------------------------- scenarios

        public void NextScenario(int step)
        {
            if (scenarios.Length == 0)
            {
                Toast("Nenhum cenário configurado.");
                return;
            }

            _scenario = ((_scenario + step) % scenarios.Length + scenarios.Length) % scenarios.Length;
            ApplyScenario(_scenario);
        }

        public void ApplyScenario(int index)
        {
            if (index < 0 || index >= scenarios.Length) return;

            _scenario = index;
            Scenario scenario = scenarios[index];

            if (scenario.speakerPoint != null && speakerBody != null)
            {
                speakerBody.SetPositionAndRotation(scenario.speakerPoint.position, scenario.speakerPoint.rotation);
            }

            facing = scenario.facing;

            if (scenario.playerPoint != null) TeleportPlayer(scenario.playerPoint.position, scenario.playerPoint.rotation);

            Toast(scenario.name);
        }

        public void PlaceSpeakerAtPlayer()
        {
            GameObject player = LocalPlayer();
            if (player == null || speakerBody == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(PlayerLook(player), Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = player.transform.forward;

            speakerBody.SetPositionAndRotation(player.transform.position, Quaternion.LookRotation(forward, Vector3.up));
            Toast("Companheiro colocado aqui. Agora se afaste.");
        }

        private void CycleFacing()
        {
            facing = (Facing)(((int)facing + 1) % 3);

            string name = facing == Facing.TowardsPlayer ? "de frente para você" : facing == Facing.AwayFromPlayer ? "de costas para você" : "parado";
            Toast("Companheiro " + name);
        }

        private void UpdateFacing()
        {
            if (speakerBody == null || facing == Facing.Fixed) return;

            GameObject player = LocalPlayer();
            if (player == null) return;

            Vector3 towards = Vector3.ProjectOnPlane(player.transform.position - speakerBody.position, Vector3.up);
            if (towards.sqrMagnitude < 0.01f) return;

            if (facing == Facing.AwayFromPlayer) towards = -towards;

            Quaternion target = Quaternion.LookRotation(towards.normalized, Vector3.up);
            speakerBody.rotation = Quaternion.RotateTowards(speakerBody.rotation, target, 240f * Time.deltaTime);
        }

        public void TeleportPlayer(Vector3 position, Quaternion rotation)
        {
            GameObject player = LocalPlayer();

            if (player == null)
            {
                Toast("Sem jogador: abra o laboratório junto com o GYM (menu Laboratório de voz).");
                return;
            }

            if (player.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.position = position;
            }

            player.transform.SetPositionAndRotation(position, rotation);

            var panTilt = player.GetComponentInChildren<CinemachinePanTilt>(true);
            if (panTilt == null) return;

            panTilt.PanAxis.Value = rotation.eulerAngles.y;
            panTilt.TiltAxis.Value = 0f;
        }

        private static GameObject LocalPlayer()
        {
            if (GymNetworkBootstrap.Instance != null && GymNetworkBootstrap.Instance.LocalPlayer != null)
            {
                return GymNetworkBootstrap.Instance.LocalPlayer;
            }

            NetworkManager network = NetworkManager.Singleton;
            if (network == null || network.LocalClient == null || network.LocalClient.PlayerObject == null) return null;

            return network.LocalClient.PlayerObject.gameObject;
        }

        private static Vector3 PlayerLook(GameObject player)
        {
            var panTilt = player.GetComponentInChildren<CinemachinePanTilt>(true);
            if (panTilt == null) return player.transform.forward;

            return Quaternion.Euler(0f, panTilt.PanAxis.Value, 0f) * Vector3.forward;
        }

        // ---------------------------------------------------------------- doors

        private void ToggleNearestDoor()
        {
            GameObject player = LocalPlayer();
            if (player == null) return;

            GymVoiceLabDoor nearest = null;
            float best = 4f;

            foreach (GymVoiceLabDoor door in _doors)
            {
                if (door == null) continue;

                float distance = Vector3.Distance(player.transform.position, door.transform.position);
                if (distance >= best) continue;

                best = distance;
                nearest = door;
            }

            if (nearest == null)
            {
                Toast("Nenhuma porta a menos de 4 m.");
                return;
            }

            nearest.Toggle();
            Toast(nearest.IsOpen ? "Porta aberta" : "Porta fechada");
        }

        private void ToggleAllDoors()
        {
            bool anyClosed = false;

            foreach (GymVoiceLabDoor door in _doors)
            {
                if (door != null && !door.IsOpen) anyClosed = true;
            }

            foreach (GymVoiceLabDoor door in _doors)
            {
                if (door != null) door.SetOpen(anyClosed);
            }

            Toast(anyClosed ? $"Todas as {_doors.Length} portas abertas" : $"Todas as {_doors.Length} portas fechadas");
        }

        // ---------------------------------------------------------------- mute

        public void SetMuteOthers(bool mute)
        {
            _muteOthers = mute;

            if (!mute)
            {
                foreach (AudioSource source in _muted)
                {
                    if (source != null) source.mute = false;
                }

                _muted.Clear();
                return;
            }

            _nextMuteScan = 0f;
            Toast("Resto do áudio silenciado: só o companheiro toca.");
        }

        private void UpdateMute()
        {
            if (!_muteOthers || Time.unscaledTime < _nextMuteScan) return;

            _nextMuteScan = Time.unscaledTime + 0.5f;
            AudioSource own = speaker != null ? speaker.Source : null;

            foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source == own || source.mute) continue;

                source.mute = true;
                _muted.Add(source);
            }
        }

        // ---------------------------------------------------------------- microphone

        private void StartRecording()
        {
            if (speaker == null) return;

            if (Microphone.devices.Length == 0)
            {
                Toast("Nenhum microfone encontrado.");
                return;
            }

            if (_recording != null) return;

            _recording = Microphone.Start(null, false, Mathf.CeilToInt(recordSeconds), 48000);
            _recordingUntil = Time.unscaledTime + recordSeconds + 0.1f;
            Toast($"Gravando {recordSeconds:0} s: fale no volume que você usa no jogo.");
        }

        private void UpdateRecording()
        {
            if (_recording == null || Time.unscaledTime < _recordingUntil) return;

            Microphone.End(null);
            speaker.UseRecording(_recording);
            _recording = null;
            Toast("Gravação pronta: o companheiro fala com a sua voz (Shift+K volta à fala gerada).");
        }

        private void UseGeneratedSpeech()
        {
            if (speaker == null) return;

            speaker.UseRecording(null);
            Toast("De volta à fala gerada.");
        }

        // ---------------------------------------------------------------- rings

        private LineRenderer MakeLine(string lineName, Color color, float width)
        {
            var go = new GameObject("Laboratorio_" + lineName);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 0;

            return line;
        }

        private void UpdateLines()
        {
            bool visible = showRings && speaker != null && speaker.IsReady && speakerBody != null;

            _rangeRing.enabled = visible;
            _whisperRing.enabled = visible;
            _speechRing.enabled = visible;
            _shoutRing.enabled = visible;
            _sightLine.enabled = visible;

            if (!visible) return;

            Vector3 centre = speakerBody.position + Vector3.up * 0.06f;

            DrawRing(_rangeRing, centre, speaker.Reading.Range);
            DrawRing(_whisperRing, centre + Vector3.up * 0.01f, speaker.WhisperRange);
            DrawRing(_speechRing, centre + Vector3.up * 0.02f, speaker.SpeechRange);
            DrawRing(_shoutRing, centre + Vector3.up * 0.03f, speaker.ShoutRange);

            AudioListener listener = FindFirstObjectByType<AudioListener>();

            if (listener == null || speaker.Mouth == null)
            {
                _sightLine.enabled = false;
                return;
            }

            _sightLine.positionCount = 2;
            _sightLine.SetPosition(0, speaker.Mouth.position);
            _sightLine.SetPosition(1, listener.transform.position + Vector3.down * 0.25f);

            Color colour = Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.8f), new Color(0.3f, 1f, 0.3f, 0.8f), speaker.Reading.Occlusion);
            _sightLine.startColor = colour;
            _sightLine.endColor = colour;
        }

        private static void DrawRing(LineRenderer line, Vector3 centre, float radius)
        {
            const int segments = 96;

            line.loop = true;
            line.positionCount = segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        // ---------------------------------------------------------------- panel

        private void Toast(string message)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + 3f;
        }

        private void OnGUI()
        {
            if (!showPanel) return;

            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
                _text = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
                _text.normal.textColor = Color.white;
            }

            string content = BuildPanel();
            const float width = 470f;
            float height = _text.CalcHeight(new GUIContent(content), width - 20f) + 16f;

            // Left, under the missions panel: the game's HUD owns the right side (inventory, chat frame).
            var rect = new Rect(10f, Screen.height * 0.16f, width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            GUI.Box(rect, GUIContent.none, _box);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, width - 20f, height - 16f), content, _text);
        }

        private string BuildPanel()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>LABORATÓRIO DE VOZ</b>   <color=#aaaaaa>(G esconde)</color>");

            if (speaker == null)
            {
                sb.AppendLine("<color=#ff6666>Sem companheiro configurado.</color>");
                return sb.ToString();
            }

            string missing = speaker.MissingReferences;
            if (missing != null)
            {
                sb.AppendLine($"<color=#ff6666>Falta {missing}. Gere o laboratório de novo.</color>");
                return sb.ToString();
            }

            LabReading r = speaker.Reading;
            string curveName = speaker.Curve == LabCurve.Current ? "<color=#7dff8a>ATUAL (igual ao jogo)</color>" : "<color=#ffd34d>ANTIGO (até 01/11)</color>";
            sb.AppendLine($"Curva: {curveName}   <color=#aaaaaa>(I troca)</color>");

            if (_scenario >= 0 && _scenario < scenarios.Length)
            {
                Scenario s = scenarios[_scenario];
                sb.AppendLine($"Cenário {_scenario + 1}/{scenarios.Length}: <b>{s.name}</b>");
                if (!string.IsNullOrEmpty(s.hint)) sb.AppendLine($"<color=#cccccc>{s.hint}</color>");
            }
            else
            {
                sb.AppendLine($"<color=#cccccc>N leva ao 1º cenário ({scenarios.Length} no total).</color>");
            }

            sb.AppendLine($"Companheiro: <b>{LevelName(speaker.Level)}</b> (U)   voz {(speaker.IsPlaying ? "tocando" : "<color=#ff9966>parada</color>")} (Y)   {FacingName()} (H)");
            sb.AppendLine($"Distância: <b>{F(r.Distance, 1)} m</b>   alcance agora: <b>{F(r.Range, 1)} m</b> (alvo {F(r.TargetRange, 1)})");
            sb.AppendLine($"Energia Vivox simulada: {F(r.Energy, 2)}   reverb da zona dele: {r.Reverb}");
            sb.AppendLine();
            sb.AppendLine("<b>O que tira volume da voz (banda média)</b>");
            sb.AppendLine(Term($"Curva de distância ({speaker.CurveDescription})", r.CurveDb));
            sb.AppendLine(Term($"Borda do alcance ({speaker.WindowDescription})", r.WindowDb));
            sb.AppendLine(Term($"Paredes/portas (oclusão {F(r.Occlusion * 100f, 0)}%, transm. {F(r.TransmissionMid, 3)})", r.WallsDb));
            sb.AppendLine(Term("Direção da boca", r.DirectivityDb));
            sb.AppendLine(Term("Ar", r.AirDb));
            sb.AppendLine(Term("Nível da fala + volume do jogador", r.SourceGainDb));
            sb.AppendLine(Term("Mixer de voz (voiceVolume)", r.MixerDb));
            sb.AppendLine($"<b>{Term("TOTAL previsto", r.PredictedDb)}</b>");

            string measured = r.EarDbfs <= -119f ? "silêncio" : $"{F(r.EarDbfs, 1)} dBFS";
            sb.AppendLine($"Medido no ouvido: <b>{measured}</b>   fonte {F(r.SourceDbfs, 1)} dBFS{(_muteOthers ? "" : "  <color=#aaaaaa>(J silencia o resto)</color>")}");

            GameObject player = LocalPlayer();
            if (player != null)
            {
                Vector3 game = player.transform.position - replicaOffset;
                sb.AppendLine($"<color=#aaaaaa>Você no Game.unity: ({F(game.x, 1)}; {F(game.z, 1)})</color>");
            }

            if (_recording != null) sb.AppendLine($"<color=#ff6666>● gravando… {F(Mathf.Max(0f, _recordingUntil - Time.unscaledTime), 1)} s</color>");
            if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil) sb.AppendLine($"<color=#ffe680>{_toast}</color>");

            sb.AppendLine();
            sb.Append("<color=#aaaaaa>N/Shift+N cenário · P pôr companheiro aqui · H virar · Y voz · U intensidade · I Atual/Antigo · " +
                      "O porta perto (Shift+O todas) · J silenciar resto · K gravar seu mic (Shift+K volta) · L anéis</color>");

            return sb.ToString();
        }

        private string FacingName()
        {
            switch (facing)
            {
                case Facing.TowardsPlayer: return "de frente";
                case Facing.AwayFromPlayer: return "de costas";
                default: return "parado";
            }
        }

        private static string LevelName(LabVoiceLevel level)
        {
            switch (level)
            {
                case LabVoiceLevel.Whisper: return "sussurro";
                case LabVoiceLevel.Shout: return "grito";
                case LabVoiceLevel.Donation: return "leitura de doação (TTS)";
                case LabVoiceLevel.Recording: return "sua gravação";
                default: return "fala";
            }
        }

        private static string Term(string label, float db)
        {
            string value = db <= -119f ? "mudo" : $"{(db > 0.05f ? "+" : "")}{F(db, 1)} dB";
            return $"  {label}: {value}";
        }

        private static string F(float value, int decimals) => value.ToString("F" + decimals, Br);
    }
}
