using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network
{
    public class ClientGameManager
    {
        private const string MENU_SCENE_NAME = "MainMenu";
        
        public async Task<bool> InitAsync()
        {
            await UnityServices.InitializeAsync();
            AuthenticationState authState = await AuthenticationController.Authenticate();

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
            SceneManager.LoadScene(MENU_SCENE_NAME);
        }
    }
}

