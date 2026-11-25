using Components;
using Missions;
using Player;
using Player.Chat;
using UI;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// While this player spectates, the HUD shows the stream of whoever is watched: their chat,
    /// their missions and their life, next to the audience bar every player shares. What only
    /// means something to a player still inside - crosshair, inventory, stamina, battery, the
    /// donation tray - is hidden.
    ///
    /// <para>It used to switch the whole gameplay HUD off on death, so a spectator watched a
    /// silent camera with no idea what the other player was doing.</para>
    /// </summary>
    public class SpectatorHud : MonoBehaviour, ISpectatorHud
    {
        [SerializeField] private HudVisibilityController hudVisibility;
        [SerializeField] private ChatUi chatUi;
        [SerializeField] private PlayerMissionHolderUi missionUi;
        [SerializeField] private HealthBarUi healthBar;

        [Tooltip("Turned off while spectating: the parts of the HUD that belong to a player still " +
                 "in the building and are not replicated for anyone else.")]
        [SerializeField] private GameObject[] hiddenWhileSpectating = System.Array.Empty<GameObject>();

        public void EnterSpectating()
        {
            foreach (GameObject element in hiddenWhileSpectating)
            {
                if (element != null) element.SetActive(false);
            }

            if (hudVisibility != null) hudVisibility.SetSpectating(true);
        }

        public void Watch(PlayerState target)
        {
            if (chatUi != null)
            {
                chatUi.ShowFeed(target != null ? target.GetComponentInChildren<ChatRelay>(true) : null);
            }

            if (missionUi != null)
            {
                missionUi.Watch(target != null ? target.GetComponentInChildren<PlayerMissionHolder>(true) : null);
            }

            if (healthBar != null)
            {
                healthBar.Watch(target != null ? target.GetComponentInChildren<Health>(true) : null);
            }
        }
    }
}
