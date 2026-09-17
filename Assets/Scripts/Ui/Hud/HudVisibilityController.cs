using System;
using System.Collections.Generic;
using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Ui
{
    /// <summary>Which toggle a binding answers to. Add a value here and a case in Subscribe.</summary>
    public enum HudToggleAction
    {
        Chat,
        Missions,
        Donations
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

        // Owner only: this HUD lives inside the player prefab and the InputReader is a shared
        // asset, so without the gate every player in the session answers the local keystroke.
        public override void OnNetworkSpawn()
        {
            if (!IsOwner || inputReader == null) return;

            inputReader.OnChatEvent += ToggleChat;
            inputReader.OnHideMissionsEvent += ToggleMissions;
            inputReader.OnHideDonateEvent += ToggleDonations;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner || inputReader == null) return;

            inputReader.OnChatEvent -= ToggleChat;
            inputReader.OnHideMissionsEvent -= ToggleMissions;
            inputReader.OnHideDonateEvent -= ToggleDonations;
        }

        public void ToggleChat() => Toggle(HudToggleAction.Chat);

        public void ToggleMissions() => Toggle(HudToggleAction.Missions);

        public void ToggleDonations() => Toggle(HudToggleAction.Donations);

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
