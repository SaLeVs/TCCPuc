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
            try
            {
                HostSingleton hostSingletonObject = Instantiate(hostSingleton);
                hostSingletonObject.CreateHost();
                
                ClientSingleton clientSingletonObject = Instantiate(clientSingleton);
                bool authenticated = await clientSingletonObject.CreateClient();
                

                if (authenticated)
                {
                    clientSingletonObject.gameManager.StartMenu();
                }
                
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
        
    }
}

