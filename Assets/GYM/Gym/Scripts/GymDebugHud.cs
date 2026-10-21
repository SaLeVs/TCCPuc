using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Components;
using Network;
using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LevelDesign.Gym
{
    /// <summary>
    /// The GYM's instrument panel: the numbers you balance by (speeds, stamina, distance to the
    /// monster, whether it sees you, how fast the gap is changing) plus a few cheat keys so a test
    /// is not ruined by a death. Reads the game through its public API and, where there is none,
    /// through reflection — no game script is modified.
    /// </summary>
    public class GymDebugHud : MonoBehaviour
    {
        private static GymDebugHud _instance;
        private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

        private static readonly FieldInfo StaminaField = typeof(PlayerRun).GetField("_stamina", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ExhaustedField = typeof(PlayerRun).GetField("_isExhausted", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VisionDistanceField = typeof(VisionSensor).GetField("distance", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VisionAngleField = typeof(VisionSensor).GetField("angle", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VisionOrientationField = typeof(VisionSensor).GetField("orientation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo VisionHeightField = typeof(VisionSensor).GetField("height", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] private float behindDistance = 10f;
        [SerializeField] private bool showHud = true;
        [SerializeField] private bool showVisionCone = true;

        private GameObject _player;
        private Rigidbody _playerBody;
        private PlayerRun _playerRun;
        private PlayerState _playerState;
        private Health _playerHealth;
        private bool _sprinting;
        private float _stamina;

        private bool _infiniteStamina;
        private bool _invincible;

        private float _distance = -1f;
        private float _closingRate;
        private bool _seen;
        private float _firstSeenAt = -1f;
        private float _chaseStartedAt = -1f;
        private float _lastChaseSeconds;
        private int _hitsTaken;

        private float _visionRange;
        private float _effectiveVision;
        private float _eyePitch;

        private string _toast;
        private float _toastUntil;

        private LineRenderer _cone;
        private GUIStyle _box;
        private GUIStyle _text;

        public static void Toast(string message)
        {
            if (_instance == null) return;
            _instance._toast = message;
            _instance._toastUntil = Time.time + 2.5f;
        }

        private void Awake() => _instance = this;

        private void OnEnable() => GymNetworkBootstrap.OnLocalPlayerSpawned += Bind;

        private void OnDisable() => GymNetworkBootstrap.OnLocalPlayerSpawned -= Bind;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Unbind();
        }

        private void Bind(GameObject player)
        {
            Unbind();

            _player = player;
            _playerBody = player.GetComponent<Rigidbody>();
            _playerRun = player.GetComponent<PlayerRun>();
            _playerState = player.GetComponent<PlayerState>();
            _playerHealth = player.GetComponent<Health>();

            if (_playerRun != null)
            {
                _stamina = _playerRun.MaxStamina;
                _playerRun.OnStaminaChanged += OnStamina;
                _playerRun.OnRunEvent += OnRun;
            }

            if (_playerHealth != null) _playerHealth.OnHealthChanged += OnHealth;
        }

        private void Unbind()
        {
            if (_playerRun != null)
            {
                _playerRun.OnStaminaChanged -= OnStamina;
                _playerRun.OnRunEvent -= OnRun;
            }

            if (_playerHealth != null) _playerHealth.OnHealthChanged -= OnHealth;
        }

        private void OnStamina(float value) => _stamina = value;
        private void OnRun(bool running) => _sprinting = running;

        private float _lastHealth = -1f;

        private void OnHealth(float value)
        {
            if (_lastHealth >= 0f && value < _lastHealth) _hitsTaken++;
            _lastHealth = value;
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            HandleKeys();
            TrackMonster();
            UpdateCone();
        }

        private void LateUpdate()
        {
            // After PlayerRun has drained this frame's stamina.
            if (_infiniteStamina && _playerRun != null)
            {
                StaminaField?.SetValue(_playerRun, _playerRun.MaxStamina);
                ExhaustedField?.SetValue(_playerRun, false);
            }

            if (_invincible && _playerHealth != null && _playerHealth.IsSpawned && _playerHealth.currentHealth.Value < _playerHealth.MaxHealth)
            {
                _playerHealth.RestoreHealth(_playerHealth.MaxHealth);
            }
        }

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var remote = GymMonsterRemote.Instance;

            if (keyboard.f1Key.wasPressedThisFrame) showHud = !showHud;
            if (keyboard.f2Key.wasPressedThisFrame && remote != null && _player != null)
            {
                remote.PlaceBehind(_player.transform, behindDistance);
                Toast($"Monstro {behindDistance:0} m atrás de você");
            }
            if (keyboard.f3Key.wasPressedThisFrame && remote != null)
            {
                remote.TogglePaused();
                Toast(remote.IsPaused ? "Monstro pausado" : "Monstro solto");
            }
            if (keyboard.f4Key.wasPressedThisFrame && _playerHealth != null)
            {
                _playerHealth.RestoreHealth(_playerHealth.MaxHealth);
                Toast("Vida cheia");
            }
            if (keyboard.f5Key.wasPressedThisFrame && GymNetworkBootstrap.Instance != null)
            {
                GymNetworkBootstrap.Instance.RespawnLocalPlayer();
                Toast("De volta ao spawn");
            }
            if (keyboard.f6Key.wasPressedThisFrame && remote != null)
            {
                remote.SendHome();
                Toast("Monstro voltou para casa");
            }
            if (keyboard.f7Key.wasPressedThisFrame)
            {
                _infiniteStamina = !_infiniteStamina;
                Toast(_infiniteStamina ? "Stamina infinita" : "Stamina normal");
            }
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                _invincible = !_invincible;
                Toast(_invincible ? "Invencível (cura automática)" : "Dano normal");
            }
            if (keyboard.f9Key.wasPressedThisFrame) showVisionCone = !showVisionCone;
            if (keyboard.f10Key.wasPressedThisFrame) StartCoroutine(RestartGym());
        }

        private void TrackMonster()
        {
            var monster = GymMonsterRemote.Instance != null ? GymMonsterRemote.Instance.Monster : null;
            if (monster == null || _player == null)
            {
                _distance = -1f;
                return;
            }

            Vector3 a = monster.transform.position;
            Vector3 b = _player.transform.position;
            a.y = b.y = 0f;
            float distance = Vector3.Distance(a, b);

            if (_distance >= 0f && Time.deltaTime > 0f)
            {
                float rate = (distance - _distance) / Time.deltaTime;
                _closingRate = Mathf.Lerp(_closingRate, rate, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }

            _distance = distance;

            bool seen = monster._playersInVision.Contains(_player.transform);
            if (seen && !_seen) _firstSeenAt = distance;
            _seen = seen;

            bool chasing = GymMonsterRemote.Instance.CurrentStateName.StartsWith("Chase") || GymMonsterRemote.Instance.CurrentStateName.StartsWith("Attack");
            if (chasing && _chaseStartedAt < 0f) _chaseStartedAt = Time.time;
            if (!chasing && _chaseStartedAt >= 0f)
            {
                _lastChaseSeconds = Time.time - _chaseStartedAt;
                _chaseStartedAt = -1f;
            }
        }

        // ---------------------------------------------------------------- vision cone

        private void UpdateCone()
        {
            var monster = GymMonsterRemote.Instance != null ? GymMonsterRemote.Instance.Monster : null;
            var sensor = monster != null ? monster.GetComponent<VisionSensor>() : null;

            if (sensor != null) MeasureEffectiveVision(sensor, monster.transform);

            if (!showVisionCone || sensor == null)
            {
                if (_cone != null) _cone.enabled = false;
                return;
            }

            if (_cone == null)
            {
                _cone = new GameObject("GymVisionCone").AddComponent<LineRenderer>();
                _cone.useWorldSpace = true;
                _cone.widthMultiplier = 0.06f;
                _cone.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                _cone.loop = true;
            }

            float range = VisionDistanceField != null ? (float)VisionDistanceField.GetValue(sensor) : 40f;
            float angle = VisionAngleField != null ? (float)VisionAngleField.GetValue(sensor) : 90f;
            var eye = VisionOrientationField?.GetValue(sensor) as Transform ?? monster.transform;

            const int arcSegments = 24;
            _cone.enabled = true;
            _cone.positionCount = arcSegments + 2;

            Vector3 origin = monster.transform.position + Vector3.up * 0.08f;
            Vector3 forward = eye.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : monster.transform.forward;

            // Drawn at the range the sensor actually reaches right now, not the nominal one.
            float drawn = Mathf.Min(range, _effectiveVision > 0f ? _effectiveVision : range);

            _cone.SetPosition(0, origin);
            for (int i = 0; i <= arcSegments; i++)
            {
                float t = -angle * 0.5f + angle * i / arcSegments;
                _cone.SetPosition(i + 1, origin + Quaternion.Euler(0f, t, 0f) * forward * drawn);
            }

            Color color = _seen ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.85f, 0.2f);
            _cone.startColor = _cone.endColor = color;
            _cone.material.color = color;
        }

        /// <summary>
        /// VisionSensor rejects targets outside a vertical band (±height/2) measured in the eye's
        /// own space. The eye rides the animated head bone, so when the head tilts, a target
        /// straight ahead leaves that band after (height/2) / sin(tilt) metres — that is the range
        /// the monster really has, whatever the distance field says.
        /// </summary>
        private void MeasureEffectiveVision(VisionSensor sensor, Transform fallback)
        {
            _visionRange = VisionDistanceField != null ? (float)VisionDistanceField.GetValue(sensor) : 0f;
            float height = VisionHeightField != null ? (float)VisionHeightField.GetValue(sensor) : 0f;
            var eye = VisionOrientationField?.GetValue(sensor) as Transform ?? fallback;

            // Positive = the head is tilted up. The sensor aims at the target collider's centre
            // (the player's chest), which sits below the eye — that offset eats into the band too.
            float up = Mathf.Asin(Mathf.Clamp(eye.forward.y, -1f, 1f));
            float targetY = _player != null && _player.TryGetComponent(out Collider body) ? body.bounds.center.y : eye.position.y - 0.75f;
            float dy = targetY - eye.position.y;
            float half = height * 0.5f;
            float sin = Mathf.Sin(up);
            float cos = Mathf.Cos(up);

            _eyePitch = up * Mathf.Rad2Deg;

            // Local height of a target straight ahead at distance D: -D·sin(up) + dy·cos(up).
            float reach = Mathf.Abs(sin) < 0.001f ? _visionRange
                : sin > 0f ? (half + dy * cos) / sin
                : (half - dy * cos) / -sin;

            _effectiveVision = Mathf.Clamp(reach, 0f, _visionRange);
        }

        // ---------------------------------------------------------------- restart

        private IEnumerator RestartGym()
        {
            Toast("Reiniciando o GYM...");
            yield return null;

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.Shutdown();
                while (networkManager != null && networkManager.ShutdownInProgress) yield return null;
                if (networkManager != null) Destroy(networkManager.gameObject);
            }

            var host = FindFirstObjectByType<HostSingleton>();
            if (host != null) Destroy(host.gameObject);

            yield return null;

            string scenePath = gameObject.scene.path;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(scenePath);
#endif
        }

        // ---------------------------------------------------------------- GUI

        private void OnGUI()
        {
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
                _text = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, wordWrap = false };
                _text.normal.textColor = Color.white;
            }

            // Top centre: the game's own HUD owns the corners (missions, chat, life, inventory).
            const float width = 494f;
            float panelBottom = 8f;

            if (showHud)
            {
                var content = new GUIContent(BuildText());
                float height = _text.CalcHeight(content, width - 24f);
                var area = new Rect((Screen.width - width) * 0.5f, 8f, width, height + 18f);

                GUI.Box(area, GUIContent.none, _box);
                GUI.Label(new Rect(area.x + 12f, area.y + 8f, width - 24f, height + 4f), content, _text);
                panelBottom = area.yMax;
            }
            else
            {
                GUI.Label(new Rect((Screen.width - 220f) * 0.5f, 8f, 220f, 22f), "<color=#aaa>GYM · F1 mostra o painel</color>", _text);
                panelBottom = 30f;
            }

            if (Time.time < _toastUntil && !string.IsNullOrEmpty(_toast))
            {
                var size = _text.CalcSize(new GUIContent(_toast));
                var rect = new Rect((Screen.width - size.x) * 0.5f - 12f, panelBottom + 8f, size.x + 24f, size.y + 12f);
                GUI.Box(rect, GUIContent.none, _box);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, size.x, size.y), _toast, _text);
            }
        }

        private string BuildText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b><color=#ffd34d>GYM · Level Design</color></b>   <color=#999>F1 esconde</color>");

            if (_player == null)
            {
                sb.AppendLine("Esperando o jogador nascer...");
                return sb.ToString();
            }

            Vector3 velocity = _playerBody != null ? _playerBody.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            string mode = _playerState != null && _playerState.IsCrouching ? "agachado" : _sprinting ? "correndo" : velocity.magnitude > 0.2f ? "andando" : "parado";
            float maxStamina = _playerRun != null ? _playerRun.MaxStamina : 1f;
            float health = _playerHealth != null && _playerHealth.IsSpawned ? _playerHealth.currentHealth.Value : 0f;

            sb.AppendLine($"<b>JOGADOR</b>  {F(velocity.magnitude)} m/s <color=#9cf>{mode}</color>   vida {health:0}/{(_playerHealth != null ? _playerHealth.MaxHealth : 0):0}   golpes {_hitsTaken}");
            sb.AppendLine($"  stamina {Bar(_stamina / maxStamina)} {F(_stamina)}/{F(maxStamina)}{(_infiniteStamina ? " <color=#7f7>∞</color>" : string.Empty)}{(_invincible ? "  <color=#7f7>invencível</color>" : string.Empty)}");

            var remote = GymMonsterRemote.Instance;
            var monster = remote != null ? remote.Monster : null;

            if (monster != null && _distance >= 0f)
            {
                var agent = monster.NavMeshAgent;
                float speed = agent != null ? agent.velocity.magnitude : 0f;
                string seen = _seen ? "<color=#ff6b5b>SIM</color>" : "<color=#8f8>não</color>";
                string gap = _closingRate > 0.15f ? $"<color=#8f8>abrindo {F(_closingRate)} m/s</color>"
                           : _closingRate < -0.15f ? $"<color=#ff6b5b>fechando {F(-_closingRate)} m/s</color>"
                           : "estável";

                sb.AppendLine($"<b>MONSTRO</b>  <color=#fc8>{remote.CurrentStateName}</color>   {F(speed)} m/s   te vê: {seen}");
                sb.AppendLine($"  distância <b>{F(_distance)} m</b>   {gap}");
                string vision = _effectiveVision < _visionRange - 0.5f
                    ? $"<color=#ff9a6b>~{F(_effectiveVision)} m efetivos</color> (cabeça {F(Mathf.Abs(_eyePitch))}° {(_eyePitch > 0f ? "p/ cima" : "p/ baixo")})"
                    : $"{F(_visionRange)} m";
                sb.AppendLine($"  visão {F(_visionRange)} m nominal · {vision}");
                sb.AppendLine($"  awareness {F(monster.MonsterAwareness.Value)} ({monster.MonsterAwareness.Level})" +
                              (_firstSeenAt >= 0f ? $"   te viu a {F(_firstSeenAt)} m" : string.Empty) +
                              (_lastChaseSeconds > 0f ? $"   última perseguição {F(_lastChaseSeconds)} s" : string.Empty));
            }
            else
            {
                sb.AppendLine("<b>MONSTRO</b>  não encontrado");
            }

            if (GymStopwatch.HasResult)
            {
                sb.Append($"<b>CRONÔMETRO</b>  {GymStopwatch.RunName}  <b>{F(GymStopwatch.Elapsed)} s</b>");
                sb.AppendLine(GymStopwatch.IsRunning ? "  <color=#9cf>rodando</color>" : string.Empty);

                var line = new StringBuilder("  ");
                foreach (var mark in GymStopwatch.Marks)
                {
                    line.Append($"{mark.Label}: {F(mark.Seconds)} s");
                    if (TryLeadingNumber(mark.Label, out float metres) && mark.Seconds > 0f) line.Append($" <color=#999>({F(metres / mark.Seconds)} m/s)</color>");
                    line.Append("   ");
                }

                if (GymStopwatch.Marks.Count > 0) sb.AppendLine(line.ToString());
            }

            sb.AppendLine("<color=#bbb>F2 monstro atrás · F3 pausa monstro · F4 cura · F5 spawn · F6 monstro p/ casa</color>");
            sb.Append("<color=#bbb>F7 stamina ∞ · F8 invencível · F9 cone de visão · F10 reinicia</color>");
            return sb.ToString();
        }

        private static string F(float value) => value.ToString("0.0", Br);

        private static string Bar(float fill)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(fill * 10f), 0, 10);
            return "<color=#7cf>" + new string('█', filled) + "</color><color=#444>" + new string('█', 10 - filled) + "</color>";
        }

        private static bool TryLeadingNumber(string label, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(label)) return false;

            int end = 0;
            while (end < label.Length && (char.IsDigit(label[end]) || label[end] == ',' || label[end] == '.')) end++;
            return end > 0 && float.TryParse(label.Substring(0, end).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
