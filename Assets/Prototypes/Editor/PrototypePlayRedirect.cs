using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Prototypes.Editor
{
    /// <summary>
    /// Pressing Play with a prototype level open (Assets/Scenes/Prototypes/GameN) starts the game
    /// the normal way, from Bootstrap, with that level already picked in the lobby field. The level
    /// is then tested through the real flow: menu, lobby, ready, network load.
    ///
    /// <para>Any other scene plays exactly as before.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class PrototypePlayRedirect
    {
        public const string PrototypeFolder = "Assets/Scenes/Prototypes/";
        private const string BootstrapScene = "Assets/Scenes/Bootstrap.unity";

        static PrototypePlayRedirect()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                Scene active = SceneManager.GetActiveScene();

                if (active.path.StartsWith(PrototypeFolder))
                {
                    EditorPrefs.SetString(PrototypeLevelPicker.EditorPrefsKey, active.name);
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScene);
                }
                else
                {
                    EditorSceneManager.playModeStartScene = null;
                    EditorPrefs.DeleteKey(PrototypeLevelPicker.EditorPrefsKey);
                }
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }
    }
}
