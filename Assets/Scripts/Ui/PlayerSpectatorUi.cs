using Player;
using TMPro;
using UnityEngine;

namespace UI
{
    public class PlayerSpectatorUi : MonoBehaviour
    {
        [SerializeField] private PlayerSpectator playerSpectatorController;
        [SerializeField] private TextMeshProUGUI playerNameText;


        private void Awake()
        {
            playerSpectatorController.OnTargetChanged += PlayerSpectatorController_OnTargetChanged;
        }

        private void PlayerSpectatorController_OnTargetChanged(string playerName)
        {
            playerNameText.text = playerName;
        }
        
        public void NextPlayerButton()
        {
            playerSpectatorController.NextPlayer();
        }

        public void PreviousPlayerButton()
        {
            playerSpectatorController.PreviousPlayer();
        }
        
        
        private void OnDestroy()
        {
            playerSpectatorController.OnTargetChanged -= PlayerSpectatorController_OnTargetChanged;
        }
        
    }
}

