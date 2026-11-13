using System;
using System.Threading.Tasks;
using Components;
using Network;
using Player;
using Systems;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tutorial
{
    /// <summary>
    /// Sobe a partida do tutorial sozinho, sem lobby: um host LAN com um jogador só.
    ///
    /// <para>Vindo do menu, usa o NetworkManager que o jogo já tem aberto. Dando Play direto na cena
    /// (no Editor), liga o NetworkManager próprio da cena, que fica desligado de propósito para nunca
    /// brigar com o do jogo. Nenhum script do jogo é alterado: o host é montado com as mesmas peças
    /// do HostGameManager (HostSingleton + NetworkServer + ConnectionPayload), como o GYM faz.</para>
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class TutorialNetworkBootstrap : MonoBehaviour
    {
        public static TutorialNetworkBootstrap Instance { get; private set; }
        public static event Action<GameObject> OnLocalPlayerSpawned;

        [Tooltip("NetworkManager desta cena. Fica desligado e só é ligado quando não existe outro (Play direto na cena).")]
        [SerializeField] private GameObject localNetworkManager;

        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Objetos que só podem acordar depois que os Unity Services subirem (TTS das doações, por exemplo).")]
        [SerializeField] private GameObject[] activateAfterServices;

        [Tooltip("O tutorial não tem sessão de voz: tira VivoxPlayer, RemoteVoiceFilter e PlayerVoiceIdentity da instância do jogador (o prefab não muda).")]
        [SerializeField] private bool stripVoiceComponents = true;

        public GameObject LocalPlayer { get; private set; }
        public Transform SpawnPoint => spawnPoint;

        /// <summary>O host subiu e o jogador local existe.</summary>
        public bool IsReady => LocalPlayer != null;

        private GameObject _temporaryTracker;
        private GameObject _temporaryHostSingleton;
        private GameObject _temporaryVoice;

        private void Awake()
        {
            Instance = this;

            // Um NetworkManager já vivo (o do jogo, vindo do menu) manda. O da cena só acorda sem ele.
            if (localNetworkManager != null && NetworkManager.Singleton == null)
            {
                localNetworkManager.SetActive(true);
            }

            EnsurePlayerTracker();

            SceneManager.sceneUnloaded += SceneManager_OnSceneUnloaded;
        }

        private async void Start()
        {
            await InitializeServicesAsync();

            if (this == null) return;

            foreach (GameObject deferred in activateAfterServices)
            {
                if (deferred != null) deferred.SetActive(true);
            }

            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null)
            {
                Debug.LogError("Tutorial: nenhum NetworkManager disponível. Gere a cena de novo em Tools/TCC/Tutorial/Gerar cena do tutorial.");
                return;
            }

            if (networkManager.IsListening || networkManager.ShutdownInProgress)
            {
                await NetworkSession.EnsureStoppedAsync();
                if (this == null) return;
            }

            if (!StartLocalHost(networkManager)) return;

            networkManager.OnClientConnectedCallback += SpawnPlayer;

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                SpawnPlayer(clientId);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            SceneManager.sceneUnloaded -= SceneManager_OnSceneUnloaded;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
            }

            CleanUpTemporaries();
        }

        private void SceneManager_OnSceneUnloaded(Scene scene)
        {
            if (scene != gameObject.scene) return;

            CleanUpTemporaries();
        }

        /// <summary>
        /// O que foi criado só para o tutorial não pode sobrar para o menu: um PlayerTracker falso
        /// em DontDestroyOnLoad faria o do menu se destruir como duplicado.
        /// </summary>
        private void CleanUpTemporaries()
        {
            if (_temporaryTracker != null) Destroy(_temporaryTracker);
            if (_temporaryHostSingleton != null) Destroy(_temporaryHostSingleton);
            if (_temporaryVoice != null) Destroy(_temporaryVoice);

            _temporaryTracker = null;
            _temporaryHostSingleton = null;
            _temporaryVoice = null;
        }

        /// <summary>
        /// O MissionManager se inscreve no PlayerTracker ao nascer e daria NullReference sem ele. Vindo
        /// do menu já existe um; no Play direto, um sem rede basta (os eventos dele nunca disparam, e
        /// por isso nenhuma missão do contrato é distribuída).
        /// </summary>
        private void EnsurePlayerTracker()
        {
            if (PlayerTracker.Instance != null) return;

            _temporaryTracker = new GameObject("PlayerTracker (tutorial)");
            _temporaryTracker.AddComponent<PlayerTracker>();
        }

        /// <summary>O menu normalmente faz isso. Sem os serviços, os singletons de Authentication/Vivox jogam exceção.</summary>
        private static async Task InitializeServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Uninitialized) return;

            try
            {
                await UnityServices.InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Tutorial: Unity Services não inicializou ({e.Message}). O tutorial segue sem eles.");
            }
        }

        private bool StartLocalHost(NetworkManager networkManager)
        {
            MultiplayerModeManager.SetLan();
            EnsureHostSession(networkManager);
            EnsureVoiceManager();
            ConnectionPayload.ApplyTo(networkManager);

            if (networkManager.StartHost()) return true;

            Debug.LogError($"Tutorial: StartHost falhou. {NetworkSession.DescribeState()}");
            return false;
        }

        /// <summary>
        /// O NetworkServer do HostGameManager só nasce dentro do StartLanHostAsync, que também carrega
        /// o Lobby. O tutorial precisa dele (aprova a conexão e guarda o nome do jogador) sem trocar de
        /// cena, então a propriedade é preenchida por reflexão - o mesmo caminho do GYM.
        /// </summary>
        private void EnsureHostSession(NetworkManager networkManager)
        {
            HostSingleton host = FindFirstObjectByType<HostSingleton>();

            if (host == null)
            {
                _temporaryHostSingleton = new GameObject("HostSingleton (tutorial)");
                host = _temporaryHostSingleton.AddComponent<HostSingleton>();
            }

            if (host.gameManager == null) host.CreateHost();
            if (host.gameManager.NetworkServer != null) return;

            var property = typeof(HostGameManager).GetProperty(nameof(HostGameManager.NetworkServer));
            property?.SetValue(host.gameManager, new NetworkServer(networkManager));
        }

        /// <summary>
        /// O menu de pausa chama VivoxManager.instance ao sair e registra erro quando não acha nenhum.
        /// Desligado, ele nunca entra em canal nenhum.
        /// </summary>
        private void EnsureVoiceManager()
        {
            if (FindFirstObjectByType<VivoxManager>(FindObjectsInactive.Include) != null) return;

            _temporaryVoice = new GameObject("VivoxManager (desligado no tutorial)");
            _temporaryVoice.SetActive(false);
            _temporaryVoice.AddComponent<VivoxManager>().enabled = false;
            _temporaryVoice.SetActive(true);
        }

        private void SpawnPlayer(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) return;
            if (networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null) return;

            Vector3 offset = new Vector3((networkManager.ConnectedClients.Count - 1) * 1.5f, 0f, 0f);
            Vector3 position = (spawnPoint != null ? spawnPoint.position : Vector3.zero) + offset;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            // Montado sob um pai desligado para os componentes de voz saírem antes de qualquer OnEnable.
            GameObject holder = new GameObject("TutorialPlayerHolder");
            holder.SetActive(false);

            GameObject player = Instantiate(playerPrefab, position, rotation, holder.transform);

            if (stripVoiceComponents)
            {
                foreach (VivoxPlayer voice in player.GetComponentsInChildren<VivoxPlayer>(true)) DestroyImmediate(voice);
                foreach (Audio.RemoteVoiceFilter filter in player.GetComponentsInChildren<Audio.RemoteVoiceFilter>(true)) DestroyImmediate(filter);
                foreach (PlayerVoiceIdentity identity in player.GetComponentsInChildren<PlayerVoiceIdentity>(true)) DestroyImmediate(identity);
            }

            player.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(player, gameObject.scene);
            Destroy(holder);

            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, destroyWithScene: true);

            if (clientId != networkManager.LocalClientId) return;

            LocalPlayer = player;
            FaceDirection(player, rotation);
            OnLocalPlayerSpawned?.Invoke(player);
        }

        /// <summary>A direção do olhar mora no eixo de pan do Cinemachine (em coordenadas de mundo), não no corpo.</summary>
        public static void FaceDirection(GameObject player, Quaternion rotation)
        {
            CinemachinePanTilt panTilt = player.GetComponentInChildren<CinemachinePanTilt>(true);
            if (panTilt == null) return;

            panTilt.PanAxis.Value = rotation.eulerAngles.y;
            panTilt.TiltAxis.Value = 0f;
        }

        /// <summary>Teleporta o jogador local (usado para refazer a fuga).</summary>
        public void Teleport(Transform target)
        {
            if (LocalPlayer == null || target == null) return;

            if (LocalPlayer.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.position = target.position;
            }

            LocalPlayer.transform.SetPositionAndRotation(target.position, target.rotation);
            FaceDirection(LocalPlayer, target.rotation);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnLocalPlayerSpawned = null;
    }
}
