using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// The one place that turns input into HUD visibility. The panels know how to hide themselves
    /// and nothing else; this decides when. Keeping the two apart is what lets the same panel be
    /// driven by a key, a menu button or a cutscene without any of them knowing about each other.
    /// </summary>
    public class HudVisibilityController : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;

        [Header("Panels")]
        [SerializeField] private HudPanel missionsPanel;
        [SerializeField] private ChatHudPanel chatPanel;
        [SerializeField] private DonationHudPanel donationPanel;

        // Owner only: this HUD lives inside the player prefab, and the InputReader is a shared
        // asset. Without the gate every player in the session would answer the local keystroke.
        public override void OnNetworkSpawn()
        {
            if (!IsOwner || inputReader == null) return;

            inputReader.OnHideMissionsEvent += ToggleMissions;
            inputReader.OnHideDonateEvent += ToggleDonations;
            inputReader.OnChatEvent += ToggleChat;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner || inputReader == null) return;

            inputReader.OnHideMissionsEvent -= ToggleMissions;
            inputReader.OnHideDonateEvent -= ToggleDonations;
            inputReader.OnChatEvent -= ToggleChat;
        }

        public void ToggleMissions() => Toggle(missionsPanel);

        public void ToggleChat() => Toggle(chatPanel);

        public void ToggleDonations() => Toggle(donationPanel);

        /// <summary>Drives every panel at once, for the fully clean frame.</summary>
        public void SetAllVisible(bool visible)
        {
            SetVisible(missionsPanel, visible);
            SetVisible(chatPanel, visible);
            SetVisible(donationPanel, visible);
        }

        private static void Toggle(HudPanel panel)
        {
            if (panel != null) panel.Toggle();
        }

        private static void SetVisible(HudPanel panel, bool visible)
        {
            if (panel != null) panel.SetVisible(visible);
        }
    }
}
