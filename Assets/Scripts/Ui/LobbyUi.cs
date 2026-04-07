using Network;
using UnityEngine;

namespace UI
{
    public class LobbyUi : MonoBehaviour
    {
        [SerializeField] private Lobby lobbyManager;
        [SerializeField] private GameObject startGameButton;
        [SerializeField] private GameObject checkReadyImage;
        
        private bool isPlayerReady;


        private void Start()
        {
            
            lobbyManager.OnJoinedLobby += SetButtonsActive;
        }
        
        public async void ToggleReady()
        {
            isPlayerReady = !isPlayerReady;
            checkReadyImage.SetActive(isPlayerReady);
            await lobbyManager.SetPlayerReady(isPlayerReady);
        }
        
        public async void StartGameButton()
        {
            await lobbyManager.StartGame();
        }

        private void SetButtonsActive()
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
        
    }

}
