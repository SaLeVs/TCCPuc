using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class ApplicationController : MonoBehaviour
    {
        [SerializeField] private ClientSingleton clientSingleton;
        [SerializeField] private HostSingleton hostSingleton;
        
        
        private async void Start()
        {
            DontDestroyOnLoad(gameObject);

            await LaunchClientAndHost();
        }
        

        private async Task LaunchClientAndHost()
        {
            ClientSingleton clientSingletonObject = null;

            try
            {
                HostSingleton hostSingletonObject = Instantiate(hostSingleton);
                hostSingletonObject.CreateHost();

                clientSingletonObject = Instantiate(clientSingleton);
                await clientSingletonObject.CreateClient();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // Always, signed in or not. The menu used to load only after a successful sign-in, so a
            // network that blocks the Unity services left a black screen - even for LAN, which needs
            // none of them. Online play reports the problem itself when it is tried.
            if (clientSingletonObject != null && clientSingletonObject.gameManager != null)
            {
                clientSingletonObject.gameManager.StartMenu();
            }
        }
        
    }
}

