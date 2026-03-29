using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class ClientSingleton : MonoBehaviour
    {
        private static ClientSingleton _instance;
        public ClientGameManager gameManager { get; private set; }

        
        public static ClientSingleton instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }
                
                _instance = FindFirstObjectByType<ClientSingleton>();

                if (_instance == null)
                {
                    Debug.LogError("ClientSingleton not found");
                    return null;
                }
                return _instance;
            }
        }
        
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        public async Task<bool> CreateClient()
        {
            gameManager = new ClientGameManager();
            return await gameManager.InitAsync();
        }
    }
}

