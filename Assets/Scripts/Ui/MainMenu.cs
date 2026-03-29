using Unity.Netcode;
using UnityEngine;

namespace UI
{
    public class MainMenu : MonoBehaviour
    {
        public void StartHost()
        {
            NetworkManager.Singleton.StartHost();
        }
    
        public void StartClient()
        {
            NetworkManager.Singleton.StartClient();
        }
    }
    
}

