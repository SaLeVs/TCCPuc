using System.Threading.Tasks;
using Systems;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network
{
    public class ClientGameManager
    {
        private const string MENU_SCENE_NAME = "MainMenu";
        private const int MAX_TRIES_TO_AUTH = 5;
        
        public async Task<bool> InitAsync()
        {
            await UnityServices.InitializeAsync();
            AuthenticationState authState = await AuthenticationController.Authenticate(MAX_TRIES_TO_AUTH);

            if (authState == AuthenticationState.Authenticated)
            {
                Debug.Log("ClientGameManager: Authenticated");
                return true;
            }
            
            Debug.Log("ClientGameManager: Failed to initialize");
            return false;
        }

        public void StartMenu()
        {
            Loader.Load(Loader.Scene.MainMenu);
        }
    }
}

