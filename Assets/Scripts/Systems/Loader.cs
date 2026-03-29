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

        public static void Load(Scene targetScene)
        {
            Loader._targetScene = targetScene;
            SceneManager.LoadScene(Scene.Loading.ToString());
        }

        public static void LoadNetwork(Scene targetScene)
        {
            // We can make here a logic for wait all players load scene
            NetworkManager.Singleton.SceneManager.LoadScene(targetScene.ToString(), LoadSceneMode.Single);
        }

        public static void LoaderCallback()
        {
            SceneManager.LoadScene(_targetScene.ToString());
        }
        
        
    }
}

