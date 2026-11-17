using Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototypes
{
    /// <summary>
    /// Editor only in practice: pressing Play with a prototype level open (Game1, Game2...) makes the
    /// match load that level instead of Game (see PrototypePlayRedirect). Anywhere else, and always in
    /// a build, the match loads the normal game. Clients need nothing: Netcode takes everyone to the
    /// scene the host loads.
    ///
    /// <para>Nothing in the lobby scene is edited: the component creates itself when the lobby loads.
    /// It used to show the host a field to type a level number; that field is gone.</para>
    /// </summary>
    public class PrototypeLevelPicker : MonoBehaviour
    {
        /// <summary>Editor only: the level whose scene was open when Play was pressed (see PrototypePlayRedirect).</summary>
        public const string EditorPrefsKey = "TCC.Prototype.PendingLevel";

        private const string LobbySceneName = "Lobby";

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
            string sceneName = pending.StartsWith("Game") ? pending : null;

            if (sceneName != null && int.TryParse(sceneName.Substring(4), out _) && Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Loader.GameSceneOverride = sceneName;
            }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            // Consumed: the next normal Play starts on the normal game again.
            if (Loader.GameSceneOverride != null) UnityEditor.EditorPrefs.DeleteKey(EditorPrefsKey);
#endif
        }
    }
}
