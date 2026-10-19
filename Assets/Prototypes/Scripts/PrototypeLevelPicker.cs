using Systems;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototypes
{
    /// <summary>
    /// Provisional, for testing the prototype levels. In the lobby, the host gets a small field in the
    /// top-right corner: typing 1 makes the match load Game1 (Assets/Scenes/Prototypes) instead of
    /// Game; leaving it empty keeps the normal game. Clients see nothing and need nothing: Netcode
    /// takes everyone to the scene the host loads.
    ///
    /// <para>Nothing in the lobby scene is edited: the field creates itself when the lobby loads.</para>
    /// </summary>
    public class PrototypeLevelPicker : MonoBehaviour
    {
        /// <summary>Editor only: the level whose scene was open when Play was pressed (see PrototypePlayRedirect).</summary>
        public const string EditorPrefsKey = "TCC.Prototype.PendingLevel";

        private const string LobbySceneName = "Lobby";

        private string _input = string.Empty;
        private string _status;
        private GUIStyle _box, _label, _field, _hint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != LobbySceneName) return;
            if (FindFirstObjectByType<PrototypeLevelPicker>() != null) return;

            var go = new GameObject("Prototype Level Picker (teste)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PrototypeLevelPicker>();
        }

        private void Awake()
        {
            // Every lobby starts on the normal game; a level only applies to the match it was picked for.
            Loader.GameSceneOverride = null;

#if UNITY_EDITOR
            string pending = UnityEditor.EditorPrefs.GetString(EditorPrefsKey, string.Empty);
            if (pending.StartsWith("Game")) _input = pending.Substring(4);
#endif
            Apply();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            // Consumed: the next normal Play starts on the normal game again.
            if (Loader.GameSceneOverride != null) UnityEditor.EditorPrefs.DeleteKey(EditorPrefsKey);
#endif
        }

        private void Apply()
        {
            string digits = _input.Trim();

            if (digits.Length == 0)
            {
                Loader.GameSceneOverride = null;
                _status = "Jogo normal (cena Game)";
                return;
            }

            string sceneName = "Game" + digits;

            if (int.TryParse(digits, out _) && Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Loader.GameSceneOverride = sceneName;
                _status = "Vai para " + sceneName;
            }
            else
            {
                Loader.GameSceneOverride = null;
                _status = sceneName + " não está no Build Settings — jogo normal";
            }
        }

        private void OnGUI()
        {
            var network = NetworkManager.Singleton;
            if (network == null || !network.IsServer) return;

            BuildStyles();

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var area = new Rect(Screen.width - 330f * scale, 20f * scale, 310f * scale, 118f * scale);

            GUI.Box(area, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(area.x + 12f * scale, area.y + 8f * scale, area.width - 24f * scale, area.height - 16f * scale));
            GUILayout.Label("Nível de teste (vazio = jogo normal)", _label);

            string typed = GUILayout.TextField(_input, 3, _field, GUILayout.Height(30f * scale));
            if (typed != _input)
            {
                _input = new string(System.Array.FindAll(typed.ToCharArray(), char.IsDigit));
                Apply();
            }

            GUILayout.Label(_status, _hint);
            GUILayout.EndArea();
        }

        private void BuildStyles()
        {
            if (_box != null) return;

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var dark = new Texture2D(1, 1);
            dark.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            dark.Apply();

            _box = new GUIStyle(GUI.skin.box) { normal = { background = dark } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * scale), fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(18 * scale), alignment = TextAnchor.MiddleCenter };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * scale), normal = { textColor = new Color(1f, 0.85f, 0.35f) } };
        }
    }
}
