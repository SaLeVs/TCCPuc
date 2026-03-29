using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class HostSingleton : MonoBehaviour
    {
        private static HostSingleton _instance;
        public HostGameManager gameManager { get; private set; }

        
        public static HostSingleton instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }
                
                _instance = FindFirstObjectByType<HostSingleton>();

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

        public void CreateHost()
        {
            gameManager = new HostGameManager();
            
        }
    }
}

