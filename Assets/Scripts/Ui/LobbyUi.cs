using Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyUi : MonoBehaviour
    {
        [SerializeField] private Lobby lobbyManager;
        [SerializeField] private GameObject startGameButton;
        
        private bool isPlayerReady;


        private void Start()
        {
            if (lobbyManager.IsHost())
            {
                startGameButton.SetActive(true);
            }
            else
            {
                startGameButton.SetActive(false);
            }
            
        }
        
        public async void ToggleReady()
        {
            isPlayerReady = !isPlayerReady;
            await lobbyManager.SetPlayerReady(isPlayerReady);
        }
        
        public async void StartGameButton()
        {
            await lobbyManager.StartGame();
        }
        
        
    }

}
