using System;
using System.Threading.Tasks;
using Components;
using Network;
using Player;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;

namespace LevelDesign.Gym
{
    /// <summary>
    /// Lets the GYM be opened and played on its own. The real flow (menu → lobby → Game) starts the
    /// host and spawns players through SpawnManager on the scene-load event; here there is no such
    /// event, so this does the same steps by hand: start a LAN host and spawn the local player.
    ///
    /// <para>Nothing in the game's scripts is changed. The host session is built from the same
    /// pieces HostGameManager uses (HostSingleton + NetworkServer + ConnectionPayload) so PlayerInfos
    /// still finds the player's name.</para>
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GymNetworkBootstrap : MonoBehaviour
    {
        public static GymNetworkBootstrap Instance { get; private set; }
        public static event Action<GameObject> OnLocalPlayerSpawned;

        [SerializeField] private NetworkManager localNetworkManager;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform spawnPoint;

        [Tooltip("O GYM não tem sessão de voz: tira VivoxPlayer, RemoteVoiceFilter e PlayerVoiceIdentity da instância do jogador (o prefab não muda).")]
        [SerializeField] private bool stripVoiceComponents = true;

        public GameObject LocalPlayer { get; private set; }
        public Transform SpawnPoint => spawnPoint;

        private void Awake()
        {
            Instance = this;

            // Arrived here from a session that is already running: keep that one, drop ours.
            if (localNetworkManager != null && NetworkManager.Singleton != null && NetworkManager.Singleton != localNetworkManager)
            {
                Destroy(localNetworkManager.gameObject);
                localNetworkManager = null;
            }
        }

        private async void Start()
        {
            await InitializeServicesAsync();

            var networkManager = NetworkManager.Singleton;

            if (networkManager == null)
            {
                Debug.LogError("GYM: nenhum NetworkManager na cena. Gere a cena de novo em Tools/TCC/Level Design/Gerar cena GYM.");
                return;
            }

            if (!networkManager.IsListening && !StartLocalHost(networkManager)) return;
            if (!networkManager.IsServer) return;

            networkManager.OnClientConnectedCallback += SpawnPlayer;

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                SpawnPlayer(clientId);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
            }
        }

        /// <summary>
        /// The menu normally does this. Without it every Authentication/Vivox singleton the player's
        /// scripts touch throws "Singleton is not initialized". No sign-in: it works offline.
        /// </summary>
        private static async Task InitializeServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Uninitialized) return;

            try
            {
                await UnityServices.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GYM: Unity Services não inicializou ({e.Message}). O GYM segue sem eles.");
            }
        }

        private static bool StartLocalHost(NetworkManager networkManager)
        {
            MultiplayerModeManager.SetLan();
            EnsureHostSession(networkManager);
            ConnectionPayload.ApplyTo(networkManager);

            if (networkManager.StartHost()) return true;

            Debug.LogError("GYM: StartHost falhou. Outra instância do jogo está usando a porta 7777?");
            return false;
        }

        /// <summary>
        /// HostGameManager only builds its NetworkServer inside StartLanHostAsync, which also loads
        /// the Lobby. The GYM needs the server (it answers connection approval and keeps the player
        /// names) without the scene change, so the property is filled through reflection.
        /// </summary>
        private static void EnsureHostSession(NetworkManager networkManager)
        {
            var host = FindFirstObjectByType<HostSingleton>();

            if (host == null)
            {
                host = new GameObject("GymHostSingleton").AddComponent<HostSingleton>();
            }

            if (host.gameManager == null) host.CreateHost();
            if (host.gameManager.NetworkServer != null) return;

            var property = typeof(HostGameManager).GetProperty(nameof(HostGameManager.NetworkServer));
            property?.SetValue(host.gameManager, new NetworkServer(networkManager));
        }

        private void SpawnPlayer(ulong clientId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) return;
            if (networkManager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null) return;

            Vector3 offset = new Vector3((networkManager.ConnectedClients.Count - 1) * 1.5f, 0f, 0f);
            Vector3 position = (spawnPoint != null ? spawnPoint.position : Vector3.zero) + offset;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            // Built under an inactive holder so the voice components can go before any OnEnable runs.
            var holder = new GameObject("GymPlayerHolder");
            holder.SetActive(false);

            var player = Instantiate(playerPrefab, position, rotation, holder.transform);

            if (stripVoiceComponents)
            {
                foreach (var voice in player.GetComponentsInChildren<VivoxPlayer>(true)) DestroyImmediate(voice);
                foreach (var filter in player.GetComponentsInChildren<Audio.RemoteVoiceFilter>(true)) DestroyImmediate(filter);
                foreach (var identity in player.GetComponentsInChildren<PlayerVoiceIdentity>(true)) DestroyImmediate(identity);
            }

            player.transform.SetParent(null, true);
            Destroy(holder);

            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, destroyWithScene: true);

            if (clientId == networkManager.LocalClientId)
            {
                LocalPlayer = player;
                FaceSpawnDirection(player, rotation);
                OnLocalPlayerSpawned?.Invoke(player);
            }
        }

        /// <summary>
        /// The look direction lives on the Cinemachine pan axis (world frame), not on the body, so a
        /// rotated spawn point alone still leaves you looking north.
        /// </summary>
        private static void FaceSpawnDirection(GameObject player, Quaternion rotation)
        {
            var panTilt = player.GetComponentInChildren<CinemachinePanTilt>(true);
            if (panTilt != null) panTilt.PanAxis.Value = rotation.eulerAngles.y;
        }

        /// <summary>Puts the local player back on the spawn point (F5 in the HUD).</summary>
        public void RespawnLocalPlayer()
        {
            if (LocalPlayer == null || spawnPoint == null) return;

            if (LocalPlayer.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.position = spawnPoint.position;
            }

            LocalPlayer.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            FaceSpawnDirection(LocalPlayer, spawnPoint.rotation);
        }
    }
}
