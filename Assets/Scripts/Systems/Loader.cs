using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Systems
{
    public static class Loader
    {
        public enum Scene
        {
            MainMenu,
            Lobby,
            Game,
            Loading,
        }

        private static Scene _targetScene;

        /// <summary>
        /// Provisional, for testing: a level from Assets/Scenes/Prototypes (Game1, Game2...) the host
        /// picked in the lobby. When set, a network load of Game goes there instead. Null means the
        /// normal game.
        /// </summary>
        public static string GameSceneOverride;

        public static void Load(Scene targetScene)
        {
            Loader._targetScene = targetScene;
            SceneManager.LoadScene(targetScene.ToString());
        }

        public static void LoadNetwork(Scene targetScene)
        {
            // We can make here a logic for wait all players load scene
            string sceneName = targetScene == Scene.Game && !string.IsNullOrEmpty(GameSceneOverride)
                ? GameSceneOverride
                : targetScene.ToString();

            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public static void LoaderCallback()
        {
            SceneManager.LoadScene(_targetScene.ToString());
        }

        public static UnityEngine.SceneManagement.Scene GetCurrentScene()
        {
            return SceneManager.GetActiveScene();
        }

        public static UnityEngine.SceneManagement.Scene GetSceneByName(Scene wantedScene)
        {
            return SceneManager.GetSceneByName(wantedScene.ToString());
        }
    }
}

