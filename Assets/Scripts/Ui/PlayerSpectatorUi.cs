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
            // Nobody being watched: no label at all.
            if (playerName == null)
            {
                playerNameText.text = string.Empty;
                return;
            }

            // The name can still be on its way over the network; same placeholder as the player list.
            string shownName = string.IsNullOrWhiteSpace(playerName) ? "..." : playerName;
            playerNameText.text = $"Assistindo: <noparse>{shownName}</noparse>";
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

