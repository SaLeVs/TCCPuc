using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class ClientSingleton : MonoBehaviour
    {
        private static ClientSingleton _instance;
        private ClientGameManager _gameManager;

        
        public static ClientSingleton Instance
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

        public async Task CreateClient()
        {
            _gameManager = new ClientGameManager();
            await _gameManager.InitAsync();
        }
    }
}

