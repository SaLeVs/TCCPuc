using System.Collections.Generic;
using Network;
using Player;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// The voice roster shown inside the pause menu.
    ///
    /// This component deliberately does NOT live on the panel it fills. Netcode skips every
    /// NetworkBehaviour whose GameObject is not activeInHierarchy when it spawns a NetworkObject -
    /// OnNetworkSpawn never runs, and IsOwner and IsSpawned are never set. Sitting under the pause
    /// canvas, which starts disabled, meant this script was silently dead. It lives on an object
    /// that is always on, and only its target content lives inside the panel.
    /// </summary>
    public class PlayerListInGameUi : NetworkBehaviour
    {
        [Tooltip("Row container inside the pause panel. Instantiating into a disabled parent is " +
                 "fine; the entries simply render once the panel opens.")]
        [SerializeField] private RectTransform playerListContent;

        [SerializeField] private PlayerListItemUi playerListItemUi;
        [SerializeField] private bool includeSelfInList = true;

        [Tooltip("Pause owner. The roster is re-read whenever the menu opens, so it is always " +
                 "correct at the moment anyone can actually see it.")]
        [SerializeField] private PlayerCamera playerCamera;

        private readonly Dictionary<string, PlayerListItemUi> _rosterEntries = new Dictionary<string, PlayerListItemUi>();

        private VivoxManager _vivox;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            if (playerCamera != null) playerCamera.OnPauseToggled += PlayerCamera_OnPauseToggled;

            Bind();
        }

        public override void OnNetworkDespawn()
        {
            if (playerCamera != null) playerCamera.OnPauseToggled -= PlayerCamera_OnPauseToggled;

            Unbind();
            ClearRoster();
        }

        private void PlayerCamera_OnPauseToggled(bool paused)
        {
            if (!paused) return;

            // Late enough that the Vivox channel is always live by now, which the spawn frame
            // never is: joining it is asynchronous and only starts as the player spawns.
            Bind();
        }

        /// <summary>
        /// Subscribes and takes a fresh reading. A missing VivoxManager is not fatal: this runs
        /// again every time the menu opens, so a manager that is not up yet stops being a
        /// permanent failure.
        /// </summary>
        private void Bind()
        {
            if (_vivox == null) _vivox = VivoxManager.instance;
            if (_vivox == null) return;

            _vivox.OnParticipantJoinedChannel -= VivoxManager_OnParticipantJoined;
            _vivox.OnParticipantLeftChannel -= VivoxManager_OnParticipantLeft;

            _vivox.OnParticipantJoinedChannel += VivoxManager_OnParticipantJoined;
            _vivox.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;

            RebuildParticipantList();
        }

        private void Unbind()
        {
            if (_vivox == null) return;

            _vivox.OnParticipantJoinedChannel -= VivoxManager_OnParticipantJoined;
            _vivox.OnParticipantLeftChannel -= VivoxManager_OnParticipantLeft;
        }

        /// <summary>
        /// Wipes and re-reads instead of topping up. Anyone who left while the menu was closed
        /// produced no event this cared about, and would otherwise linger as a ghost.
        /// </summary>
        private void RebuildParticipantList()
        {
            ClearRoster();

            if (_vivox == null) return;

            foreach (VivoxParticipant participant in _vivox.CurrentParticipants)
            {
                VivoxManager_OnParticipantJoined(participant);
            }
        }

        private void VivoxManager_OnParticipantJoined(VivoxParticipant participant)
        {
            if (!includeSelfInList && participant.IsSelf) return;
            if (_rosterEntries.ContainsKey(participant.PlayerId)) return;

            PlayerListItemUi entry = Instantiate(playerListItemUi, playerListContent);
            entry.Setup(participant, OnVolumeChanged, OnMuteChanged);
            _rosterEntries[participant.PlayerId] = entry;
        }

        private void VivoxManager_OnParticipantLeft(VivoxParticipant participant)
        {
            if (!_rosterEntries.TryGetValue(participant.PlayerId, out PlayerListItemUi entry)) return;

            Destroy(entry.gameObject);
            _rosterEntries.Remove(participant.PlayerId);
        }

        private void ClearRoster()
        {
            foreach (PlayerListItemUi entry in _rosterEntries.Values)
            {
                if (entry != null) Destroy(entry.gameObject);
            }

            _rosterEntries.Clear();
        }

        private void OnVolumeChanged(string playerId, int volume)
        {
            VivoxManager.instance?.SetParticipantVolume(playerId, volume);
        }

        private void OnMuteChanged(string playerId, bool isMuted)
        {
            VivoxManager.instance?.SetParticipantLocalMute(playerId, isMuted);
        }
    }
}
