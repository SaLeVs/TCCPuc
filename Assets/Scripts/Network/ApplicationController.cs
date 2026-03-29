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
                ClientSingleton clientSingletonObject = Instantiate(clientSingleton);
                await clientSingletonObject.CreateClient();
                
                HostSingleton hostSingletonObject = Instantiate(hostSingleton);
                hostSingletonObject.CreateHost();
                
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
        
    }
}

