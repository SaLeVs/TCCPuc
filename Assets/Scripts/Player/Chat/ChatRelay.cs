using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Player.Chat
{
    /// <summary>
    /// This player's stream chat, shared so whoever spectates them sees the same lines.
    ///
    /// <para>The chat is made up on each player's own machine (<see cref="ChatManager"/>: what their
    /// camera saw, what happened to them), so nobody else had it. The owner sends every line it shows
    /// to the other machines, and each machine keeps the last few per player - a spectator switching
    /// to someone finds their chat already going instead of an empty box.</para>
    ///
    /// <para>Lives on the player's root, not inside the HUD: Netcode skips a NetworkBehaviour whose
    /// object is inactive when the player spawns, and the HUD canvases come and go.</para>
    /// </summary>
    public class ChatRelay : NetworkBehaviour
    {
        /// <summary>Lines kept per player, here and on every other machine.</summary>
        public const int HistorySize = 12;

        /// <summary>A line of this player's chat, on every machine: their own the moment it shows, everyone else's as it arrives.</summary>
        public event Action<string, string> OnLine;

        [Tooltip("This player's chat. Found among the children when left empty.")]
        [SerializeField] private ChatManager chatManager;

        [SerializeField] private PlayerState playerState;

        public IReadOnlyList<ChatLine> History => _history;

        private readonly List<ChatLine> _history = new List<ChatLine>(HistorySize);

        public readonly struct ChatLine
        {
            public readonly string Viewer;
            public readonly string Message;

            public ChatLine(string viewer, string message)
            {
                Viewer = viewer;
                Message = message;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            if (chatManager == null) chatManager = GetComponentInChildren<ChatManager>(true);
            if (playerState == null) playerState = GetComponent<PlayerState>();

            if (chatManager != null) chatManager.OnMessageSent += ChatManager_OnMessageSent;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && chatManager != null) chatManager.OnMessageSent -= ChatManager_OnMessageSent;

            _history.Clear();
        }

        private void ChatManager_OnMessageSent(string viewer, string message)
        {
            Store(viewer, message);

            // Nobody spectates someone out of the match: their chat stays on their own screen.
            if (playerState != null && playerState.IsOutOfPlay) return;
            if (!IsSpawned) return;

            ShareLineRpc(viewer, message);
        }

        // From the owner, through the server, to everyone else.
        [Rpc(SendTo.NotMe)]
        private void ShareLineRpc(string viewer, string message)
        {
            Store(viewer, message);
        }

        private void Store(string viewer, string message)
        {
            if (_history.Count >= HistorySize) _history.RemoveAt(0);

            _history.Add(new ChatLine(viewer, message));
            OnLine?.Invoke(viewer, message);
        }
    }
}
