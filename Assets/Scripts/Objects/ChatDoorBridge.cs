using Chat;
using Unity.Netcode;
using UnityEngine;

namespace Objects
{
    /// <summary>
    /// Makes the chat notice when a door refuses to open.
    ///
    /// <para>Hooks the blocked-door sound rather than the interaction itself. That sound is already
    /// raised on every client through an RPC, so it is the one version of the event the chat can
    /// see without the door needing to know who pressed the key.</para>
    ///
    /// <para>It is filtered by distance, not by who interacted, and deliberately so: viewers are
    /// watching a screen. A door rattling in front of the streamer is worth a comment whoever
    /// pulled on it, and one rattling across the map is not.</para>
    /// </summary>
    public class ChatDoorBridge : MonoBehaviour
    {
        /// <summary>How close the door has to be to the local player to be worth commenting on.</summary>
        private const float CommentRange = 14f;

        /// <summary>Re-resolving the player object costs a lookup; it does not change often.</summary>
        private const float PlayerRefreshInterval = 2f;

        private Transform _localPlayer;
        private float _nextPlayerRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject host = new GameObject(nameof(ChatDoorBridge));

            host.AddComponent<ChatDoorBridge>();

            DontDestroyOnLoad(host);
        }

        private void OnEnable()
        {
            Door.OnDoorBlockedSound += Door_OnBlocked;
        }

        private void OnDisable()
        {
            Door.OnDoorBlockedSound -= Door_OnBlocked;
        }

        private void Door_OnBlocked(Vector3 position)
        {
            Transform player = ResolveLocalPlayer();

            if (player == null) return;

            float distance = Vector3.Distance(player.position, position);

            if (distance > CommentRange) return;

            // Closer means more certainly the streamer's own problem, so chat is more sure of itself.
            float intensity = Mathf.Lerp(0.6f, 0.35f, Mathf.Clamp01(distance / CommentRange));

            ChatStimulusBus.Raise(ChatTopics.HintDoorLocked, intensity);
        }

        private Transform ResolveLocalPlayer()
        {
            if (_localPlayer != null && Time.time < _nextPlayerRefresh) return _localPlayer;

            _nextPlayerRefresh = Time.time + PlayerRefreshInterval;

            NetworkManager manager = NetworkManager.Singleton;

            if (manager == null || manager.SpawnManager == null) return null;

            NetworkObject playerObject = manager.SpawnManager.GetPlayerNetworkObject(manager.LocalClientId);

            _localPlayer = playerObject == null ? null : playerObject.transform;

            return _localPlayer;
        }
    }
}
