using System;
using System.Collections.Generic;
using Inputs;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace Ui
{
    /// <summary>Which toggle a binding answers to. Add a value here and a case in Subscribe.</summary>
    public enum HudToggleAction
    {
        Chat,
        Missions
    }

    /// <summary>
    /// The one place that turns input into HUD visibility. The panels know how to fold themselves
    /// away and nothing else; this decides when. Keeping the two apart is what lets the same panel
    /// be driven by a key, a menu button or a cutscene without any of them knowing about each other.
    /// </summary>
    public class HudVisibilityController : NetworkBehaviour
    {
        [Serializable]
        private class Binding
        {
            public HudToggleAction action;
            public HudAnimationPanel panel;
        }

        [SerializeField] private InputReader inputReader;

        [Tooltip("One row per panel. Several rows may share an action if two panels should fold " +
                 "away on the same key.")]
        [SerializeField] private List<Binding> bindings = new();

        [Tooltip("The donation tray does not toggle, it cycles through what is pending, so it " +
                 "gets its own reference instead of a binding row.")]
        [SerializeField] private Missions.Donations.DonationUIController donations;

        [Header("Gameplay lock")]
        [Tooltip("Player whose locked state suppresses the HUD.")]
        [SerializeField] private PlayerState playerState;

        [Tooltip("Turned off outright while the player is locked into a board or a mission panel. " +
                 "SetActive rather than the panels' own hide, because the panels must come back " +
                 "exactly as the player left them - a collapsed chat stays collapsed.")]
        [SerializeField] private GameObject gameplayCanvas;

        [Tooltip("Pause owner. The HUD hides while the pause menu is up, and that has to be a " +
                 "reason of its own - leaving the pause while still locked at a board must not " +
                 "bring the HUD back.")]
        [SerializeField] private PlayerCamera playerCamera;

        private bool _isLocked;
        private bool _isPaused;

        // Owner only: this HUD lives inside the player prefab and the InputReader is a shared
        // asset, so without the gate every player in the session answers the local keystroke.
        public override void OnNetworkSpawn()
        {
            if (!IsOwner || inputReader == null) return;

            inputReader.OnChatEvent += ToggleChat;
            inputReader.OnHideMissionsEvent += ToggleMissions;
            inputReader.OnHideDonateEvent += ToggleDonations;

            if (playerState != null) playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            if (playerCamera != null) playerCamera.OnPauseToggled += PlayerCamera_OnPauseToggled;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner || inputReader == null) return;

            inputReader.OnChatEvent -= ToggleChat;
            inputReader.OnHideMissionsEvent -= ToggleMissions;
            inputReader.OnHideDonateEvent -= ToggleDonations;

            if (playerState != null) playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
            if (playerCamera != null) playerCamera.OnPauseToggled -= PlayerCamera_OnPauseToggled;
        }


        private void PlayerState_OnPlayerLocked(bool locked)
        {
            _isLocked = locked;
            ApplyHudVisibility();
        }

        private void PlayerCamera_OnPauseToggled(bool paused)
        {
            _isPaused = paused;
            ApplyHudVisibility();
        }

        /// <summary>
        /// Two independent reasons to suppress the HUD, so dropping one never undoes the other.
        /// That is the same trap the cursor used to fall into: closing the pause while still
        /// locked at a board would assert "everything is back to normal" and it was not.
        /// </summary>
        private void ApplyHudVisibility()
        {
            if (gameplayCanvas == null) return;

            bool shouldBeActive = !_isLocked && !_isPaused;
            if (gameplayCanvas.activeSelf == shouldBeActive) return;

            gameplayCanvas.SetActive(shouldBeActive);
        }

        public void ToggleChat() => Toggle(HudToggleAction.Chat);

        public void ToggleMissions() => Toggle(HudToggleAction.Missions);

        public void ToggleDonations()
        {
            if (donations != null) donations.CycleNext();
        }

        /// <summary>Drives every bound panel at once, for the fully clean frame.</summary>
        public void SetAllVisible(bool visible)
        {
            foreach (Binding binding in bindings)
            {
                if (binding?.panel != null) binding.panel.SetVisible(visible);
            }
        }

        private void Toggle(HudToggleAction action)
        {
            foreach (Binding binding in bindings)
            {
                if (binding?.panel == null || binding.action != action) continue;

                binding.panel.Toggle();
            }
        }
    }
}
