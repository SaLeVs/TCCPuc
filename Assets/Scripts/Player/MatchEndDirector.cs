using System.Collections;
using System.Collections.Generic;
using Enums;
using Interfaces;
using Systems;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Player
{
    /// <summary>
    /// Decides when a match is over and takes everyone to the Win or Lose scene together.
    ///
    /// <para>The match is over once nobody is left playing: every player has either escaped or died
    /// (a player who leaves the session simply stops counting). At least one escape makes it a
    /// victory for whoever got out; nobody out is a defeat. The server decides, sends every peer the
    /// result (who survived, who was there, how long it took), lets the screen fade for a moment so
    /// the last death or escape is seen, and then loads the end scene over the network.</para>
    ///
    /// <para>Lives on its own for the whole run (created at startup), so no scene or prefab has to
    /// carry it. It only acts in a match scene (<see cref="SceneNames.IsGame"/>, not the tutorial);
    /// anywhere else <see cref="TryEvaluateServer"/> says so and the caller keeps its old
    /// behaviour.</para>
    /// </summary>
    public class MatchEndDirector : MonoBehaviour
    {
        public const string WinSceneName = "Win";
        public const string LoseSceneName = "Lose";

        private const string ResultMessage = "MatchEnd.Result";

        // From the last death/escape to the end scene. A death gets longer so the death camera is seen.
        private const float VictoryDelay = 2.5f;
        private const float DefeatDelay = 4f;
        private const float FadeDuration = 1.2f;

        private static MatchEndDirector _instance;

        private NetworkManager _networkManager;
        private CustomMessagingManager _messaging;

        private bool _inMatch;
        private bool _ended;
        private float _matchStartTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            GameObject go = new GameObject(nameof(MatchEndDirector));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MatchEndDirector>();
        }

        /// <summary>
        /// Server side, after a player died, escaped or left: ends the match if nobody is left
        /// playing. Returns false when this is not a match scene (or not the server), so the caller
        /// can fall back to what it did before; true means the director took care of it, whether
        /// the match ended or goes on.
        /// </summary>
        public static bool TryEvaluateServer()
        {
            if (_instance == null || !_instance._inMatch) return false;

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) return false;

            _instance.Evaluate(networkManager);
            return true;
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Deactivate();

            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;

            _inMatch = IsMatchScene(scene.name);
            _ended = false;

            if (_inMatch)
            {
                _matchStartTime = Time.unscaledTime;
                MatchResult.Clear();
            }
        }

        private static bool IsMatchScene(string sceneName) =>
            SceneNames.IsGame(sceneName) && !sceneName.Contains("Tutorial");

        private void Update()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            bool isRunning = networkManager != null && networkManager.IsListening &&
                             networkManager.CustomMessagingManager != null &&
                             (networkManager.IsServer || networkManager.IsConnectedClient);

            if (!isRunning)
            {
                if (_networkManager != null) Deactivate();
                return;
            }

            if (networkManager != _networkManager || networkManager.CustomMessagingManager != _messaging)
            {
                Activate(networkManager);
            }
        }

        private void Activate(NetworkManager networkManager)
        {
            Deactivate();

            _networkManager = networkManager;
            _messaging = networkManager.CustomMessagingManager;
            _messaging.RegisterNamedMessageHandler(ResultMessage, OnResultMessage);
            _networkManager.OnClientDisconnectCallback += OnClientDisconnect;
        }

        private void Deactivate()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnect;

                if (_messaging != null && _networkManager.CustomMessagingManager == _messaging)
                {
                    _messaging.UnregisterNamedMessageHandler(ResultMessage);
                }
            }

            _networkManager = null;
            _messaging = null;
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (_networkManager == null || !_networkManager.IsServer) return;
            if (!_inMatch || _ended) return;

            // The leaving player's object is still around during the callback: look a frame later.
            StartCoroutine(EvaluateNextFrame());
        }

        private IEnumerator EvaluateNextFrame()
        {
            yield return null;
            TryEvaluateServer();
        }

        private void Evaluate(NetworkManager networkManager)
        {
            if (_ended) return;

            List<string> players = new List<string>();
            List<string> survivors = new List<string>();

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                NetworkObject playerObject = networkManager.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObject == null) continue;
                if (!playerObject.TryGetComponent(out PlayerState state)) continue;

                // Someone is still in the building: the match goes on.
                if (!state.IsDead && !state.HasEscapedServerSide) return;

                string playerName = DisplayNameOf(state, clientId);
                players.Add(playerName);

                if (state.HasEscapedServerSide) survivors.Add(playerName);
            }

            if (players.Count == 0) return;

            _ended = true;

            bool isVictory = survivors.Count > 0;
            float duration = Time.unscaledTime - _matchStartTime;
            float delay = isVictory ? VictoryDelay : DefeatDelay;

            SendResult(networkManager, isVictory, duration, delay, survivors, players);
            StartCoroutine(LoadEndSceneAfter(networkManager, isVictory ? WinSceneName : LoseSceneName, delay));
        }

        private static string DisplayNameOf(PlayerState state, ulong clientId)
        {
            string playerName = state.PlayerInfos != null ? state.PlayerInfos.DisplayName : null;
            return string.IsNullOrWhiteSpace(playerName) ? $"Jogador {clientId + 1}" : playerName.Trim();
        }

        private void SendResult(NetworkManager networkManager, bool isVictory, float duration, float delay,
            List<string> survivors, List<string> players)
        {
            string survivorList = string.Join("\n", survivors);
            string playerList = string.Join("\n", players);

            List<ulong> remoteClients = new List<ulong>();
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId != NetworkManager.ServerClientId) remoteClients.Add(clientId);
            }

            if (remoteClients.Count > 0)
            {
                int size = 64 + (survivorList.Length + playerList.Length) * 4;
                using FastBufferWriter writer = new FastBufferWriter(size, Allocator.Temp, 64 * 1024);
                writer.WriteValueSafe(isVictory);
                writer.WriteValueSafe(duration);
                writer.WriteValueSafe(delay);
                writer.WriteValueSafe(survivorList);
                writer.WriteValueSafe(playerList);
                _messaging.SendNamedMessage(ResultMessage, remoteClients, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }

            // The host's own client never receives what the server sends: hand it over directly.
            if (networkManager.IsHost)
            {
                ApplyLocally(isVictory, duration, delay, survivorList, playerList);
            }
        }

        private void OnResultMessage(ulong senderId, FastBufferReader reader)
        {
            if (senderId != NetworkManager.ServerClientId) return;

            reader.ReadValueSafe(out bool isVictory);
            reader.ReadValueSafe(out float duration);
            reader.ReadValueSafe(out float delay);
            reader.ReadValueSafe(out string survivorList);
            reader.ReadValueSafe(out string playerList);

            ApplyLocally(isVictory, duration, delay, survivorList, playerList);
        }

        private void ApplyLocally(bool isVictory, float duration, float delay, string survivorList, string playerList)
        {
            _ended = true;
            MatchResult.Set(isVictory, duration, Split(survivorList), Split(playerList));

            NetworkObject localPlayer = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
            if (localPlayer != null && localPlayer.TryGetComponent(out PlayerState state))
            {
                state.SetInputLocked(InputLockReason.EndGame, true);
            }

            ScreenFade.Create(0f).FadeTo(1f, FadeDuration, Mathf.Max(0f, delay - FadeDuration));
        }

        private static IEnumerable<string> Split(string list) =>
            string.IsNullOrEmpty(list) ? new string[0] : list.Split('\n');

        private IEnumerator LoadEndSceneAfter(NetworkManager networkManager, string sceneName, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (networkManager == null || !networkManager.IsServer || networkManager.SceneManager == null) yield break;

            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"MatchEndDirector: could not load {sceneName} ({status}). Is it in the Build Settings?");
            }
        }
    }
}
