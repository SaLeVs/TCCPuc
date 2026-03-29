using Network;
using UnityEngine;

namespace UI
{
    public class LobbyUi : MonoBehaviour
    {
        [SerializeField] private Lobby lobbyManager;
        
        private bool isPlayerReady;
        
        
        public async void ToggleReady()
        {
            isPlayerReady = !isPlayerReady;
            await lobbyManager.SetPlayerReady(isPlayerReady);
        }
        
        
    }

}
