using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class ApplicationController : MonoBehaviour
    {
        [SerializeField] private ClientSingleton clientSingleton;
        
        
        private async void Start()
        {
            DontDestroyOnLoad(gameObject);

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                await LaunchDedicatedServer();
            }
            else
            {
                await LaunchClientAndHost();
            }
        }
        
        private Task LaunchDedicatedServer()
        {
            
        }

        private async Task LaunchClientAndHost()
        {
            try
            {
                ClientSingleton clientSingletonObject = Instantiate(clientSingleton);
                await clientSingletonObject.CreateClient();
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
        
    }
}

