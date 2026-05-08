using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Systems
{
    public class ReadyBoard : MonoBehaviour
    {
        [SerializeField] private PlayersReady playersReady;
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private TextMeshProUGUI playersReadyCount;
    
        private bool _isReady = false;

        
        private void Awake()
        {
            UpdateButtonVisual();
            playersReady.OnReadyCountChanged += PlayersReady_UpdateCountText;
        }

        public void SetPlayerReady()
        {
            _isReady = !_isReady;

            if (_isReady)
            {
                playersReady.SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                playersReady.SetPlayerNotReadyServerRpc(NetworkManager.Singleton.LocalClientId);
            }
        
            UpdateButtonVisual();
        }

        private void UpdateButtonVisual()
        {
            buttonText.text = _isReady ? "Ready!" : "Not Ready";
        }

        private void PlayersReady_UpdateCountText(int readyCount, int totalCount)
        {
            buttonText.text = readyCount + " / " + totalCount;
        }
        
        
    }
}
