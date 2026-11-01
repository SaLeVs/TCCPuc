using System.Collections.Generic;
using Network;
using Player;
using Unity.Netcode;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// The roster shown inside the pause menu: everyone in the match, with the volume and mute this
    /// player chose for each of them.
    ///
    /// <para>It lists the match's players, not the voice channel's participants. Vivox drops whoever
    /// leaves its channel — the mic test used to, and so did walking past the channel's audible
    /// distance — and a roster read from it lost people who were still playing, which also gave away
    /// who was near. Vivox now only says whether each of them has voice at the moment.</para>
    ///
    /// <para>This component deliberately does NOT live on the panel it fills. Netcode skips every
    /// NetworkBehaviour whose GameObject is not activeInHierarchy when it spawns a NetworkObject -
    /// OnNetworkSpawn never runs, and IsOwner and IsSpawned are never set. Sitting under the pause
    /// canvas, which starts disabled, meant this script was silently dead. It lives on an object
    /// that is always on, and only its target content lives inside the panel.</para>
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

        [Tooltip("Seconds between refreshes while the menu is open: players arriving or leaving, " +
                 "and who has voice.")]
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

        private readonly Dictionary<ulong, PlayerListItemUi> _rosterEntries = new Dictionary<ulong, PlayerListItemUi>();
        private readonly HashSet<ulong> _seen = new HashSet<ulong>();
        private readonly List<ulong> _gone = new List<ulong>();

        private VivoxManager _vivox;
        private bool _isOpen;
        private float _nextRefreshTime;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            if (playerCamera != null) playerCamera.OnPauseToggled += PlayerCamera_OnPauseToggled;
        }

        public override void OnNetworkDespawn()
        {
            if (playerCamera != null) playerCamera.OnPauseToggled -= PlayerCamera_OnPauseToggled;

            _isOpen = false;
            ClearRoster();
        }

        private void PlayerCamera_OnPauseToggled(bool paused)
        {
            _isOpen = paused;

            if (!paused)
            {
                // The sliders only write to PlayerPrefs in memory; closing the menu is when they are
                // done moving, so that is when the choices go to disk and survive a crash.
                PlayerPrefs.Save();
                return;
            }

            Refresh();
        }

        private void Update()
        {
            if (!_isOpen || Time.unscaledTime < _nextRefreshTime) return;

            Refresh();
        }

        /// <summary>
        /// Adds whoever spawned, drops whoever left and updates who has voice. Rows are kept, not
        /// rebuilt, so a slider being dragged never disappears from under the mouse.
        /// </summary>
        private void Refresh()
        {
            _nextRefreshTime = Time.unscaledTime + refreshInterval;

            // Looked up quietly: VivoxManager.instance logs an error every time it is missing, and
            // there is no voice at all in LAN matches or the GYM.
            if (_vivox == null) _vivox = FindFirstObjectByType<VivoxManager>();

            PlayerInfos[] players = FindObjectsByType<PlayerInfos>(FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

            _seen.Clear();
            int row = 0;

            foreach (PlayerInfos player in players)
            {
                if (!player.IsSpawned) continue;

                ulong clientId = player.OwnerClientId;
                bool isSelf = clientId == NetworkManager.LocalClientId;

                if (!includeSelfInList && isSelf) continue;

                _seen.Add(clientId);

                if (!_rosterEntries.TryGetValue(clientId, out PlayerListItemUi entry))
                {
                    entry = Instantiate(playerListItemUi, playerListContent);
                    entry.Setup(isSelf, OnVolumeChanged, OnMuteChanged);
                    _rosterEntries[clientId] = entry;
                }

                PlayerVoiceIdentity identity = player.GetComponentInChildren<PlayerVoiceIdentity>(true);
                string playerId = identity != null ? identity.VivoxPlayerId : null;
                string displayName = player.PlayerName.Value.ToString();

                entry.Refresh(string.IsNullOrEmpty(displayName) ? "..." : displayName, playerId,
                    _vivox != null && _vivox.IsInCurrentChannel(playerId),
                    _vivox != null ? _vivox.GetParticipantVolume(playerId) : 0,
                    _vivox != null && _vivox.IsParticipantLocallyMuted(playerId));

                entry.transform.SetSiblingIndex(row++);
            }

            _gone.Clear();

            foreach (ulong clientId in _rosterEntries.Keys)
            {
                if (!_seen.Contains(clientId)) _gone.Add(clientId);
            }

            foreach (ulong clientId in _gone)
            {
                if (_rosterEntries[clientId] != null) Destroy(_rosterEntries[clientId].gameObject);
                _rosterEntries.Remove(clientId);
            }
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
            if (_vivox != null) _vivox.SetParticipantVolume(playerId, volume);
        }

        private void OnMuteChanged(string playerId, bool isMuted)
        {
            if (_vivox != null) _vivox.SetParticipantLocalMute(playerId, isMuted);
        }
    }
}
