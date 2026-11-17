using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// Grava em arquivo tudo o que ajuda a entender uma falha de conexão LAN: avisos, erros e exceções
    /// do jogo, eventos do NetworkManager (início, queda, motivo da desconexão, falha de transporte),
    /// a configuração do transporte (IP, porta, endereço de escuta) e se a porta estava livre antes de
    /// hospedar. Sobe sozinho, sem precisar estar em cena.
    ///
    /// Build: pasta "LanLogs" ao lado do .exe (ou, sem permissão de escrita, em persistentDataPath/LanLogs).
    /// Editor: pasta Logs/LanLogs do projeto. Um arquivo por execução, mantendo os 20 mais recentes.
    /// </summary>
    public sealed class LanDiagnosticsLog : MonoBehaviour
    {
        private const int MaxFiles = 20;

        private static readonly object FileLock = new object();
        private static StreamWriter _writer;
        private static LanDiagnosticsLog _instance;

        public static string FilePath { get; private set; }

        private NetworkManager _subscribedManager;
        private bool _wasListening;
        private string _lastTransportConfig;
        private int _lastProbedPort = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            OpenFile();

            GameObject go = new GameObject("[LanDiagnosticsLog]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LanDiagnosticsLog>();
        }

        // ---------------------------------------------------------------- arquivo

        private static void OpenFile()
        {
            string dir = PickDirectory();
            if (dir == null) return;

            try
            {
                PruneOldFiles(dir);

                FilePath = Path.Combine(dir, $"lan_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Environment.MachineName}.log");
                _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };

                Application.logMessageReceivedThreaded += OnUnityLog;
                Application.quitting += OnQuit;

                WriteHeader();
            }
            catch (Exception e)
            {
                _writer = null;
                Debug.LogWarning($"LanDiagnosticsLog: não foi possível criar o arquivo de log ({e.Message}).");
            }
        }

        private static string PickDirectory()
        {
            string[] candidates = Application.isEditor
                ? new[] { Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "LanLogs") }
                : new[]
                {
                    Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "LanLogs"),
                    Path.Combine(Application.persistentDataPath, "LanLogs")
                };

            foreach (string dir in candidates)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    string probe = Path.Combine(dir, ".write_test");
                    File.WriteAllText(probe, "");
                    File.Delete(probe);
                    return dir;
                }
                catch
                {
                    // sem permissão aqui: tenta o próximo
                }
            }

            return null;
        }

        private static void PruneOldFiles(string dir)
        {
            FileInfo[] old = new DirectoryInfo(dir).GetFiles("lan_*.log")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(MaxFiles - 1)
                .ToArray();

            foreach (FileInfo f in old)
            {
                try { f.Delete(); } catch { /* em uso por outra instância */ }
            }
        }

        private static void Write(string category, string message)
        {
            lock (FileLock)
            {
                if (_writer == null) return;

                try
                {
                    _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}");
                }
                catch
                {
                    // disco cheio / arquivo fechado: não derruba o jogo
                }
            }
        }

        private static void WriteHeader()
        {
            Write("INFO", "==== Diagnóstico LAN ====");
            Write("INFO", $"Jogo {Application.productName} v{Application.version} | Unity {Application.unityVersion} | Editor={Application.isEditor}");
            Write("INFO", $"Máquina {Environment.MachineName} | {SystemInfo.operatingSystem}");
            Write("INFO", $"Processo {System.Diagnostics.Process.GetCurrentProcess().Id} | Arquivo {FilePath}");
            Write("INFO", $"Interfaces de rede: {DescribeLocalInterfaces()}");
        }

        private static void OnQuit()
        {
            Write("INFO", "Jogo fechado.");

            lock (FileLock)
            {
                Application.logMessageReceivedThreaded -= OnUnityLog;
                _writer?.Dispose();
                _writer = null;
            }
        }

        // ---------------------------------------------------------------- logs do Unity

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log) return;

            var sb = new StringBuilder(condition);

            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                if (!string.IsNullOrWhiteSpace(stackTrace))
                {
                    sb.Append("\n    ").Append(stackTrace.Trim().Replace("\n", "\n    "));
                }
            }

            Write(type.ToString().ToUpperInvariant(), sb.ToString());
        }

        // ---------------------------------------------------------------- NetworkManager

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;

            if (nm != _subscribedManager)
            {
                Unsubscribe();
                if (nm != null) Subscribe(nm);
            }

            if (nm == null) return;

            string config = DescribeTransport(nm);
            if (config != _lastTransportConfig)
            {
                _lastTransportConfig = config;
                Write("NET", $"Transporte configurado: {config}");
            }

            if (!nm.IsListening && !nm.ShutdownInProgress)
            {
                ProbePortIfChanged(nm);
            }

            if (nm.IsListening != _wasListening)
            {
                _wasListening = nm.IsListening;
                Write("NET", (_wasListening ? "Sessão ATIVA. " : "Sessão PARADA. ") + DescribeState(nm));
            }
        }

        private void Subscribe(NetworkManager nm)
        {
            _subscribedManager = nm;
            nm.OnServerStarted += OnServerStarted;
            nm.OnServerStopped += OnServerStopped;
            nm.OnClientStarted += OnClientStarted;
            nm.OnClientStopped += OnClientStopped;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            nm.OnTransportFailure += OnTransportFailure;
            Write("NET", "NetworkManager encontrado.");
        }

        private void Unsubscribe()
        {
            NetworkManager nm = _subscribedManager;
            _subscribedManager = null;
            if (nm == null) return;

            nm.OnServerStarted -= OnServerStarted;
            nm.OnServerStopped -= OnServerStopped;
            nm.OnClientStarted -= OnClientStarted;
            nm.OnClientStopped -= OnClientStopped;
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnTransportFailure -= OnTransportFailure;
        }

        private void OnServerStarted() => Write("NET", $"Servidor iniciado. {DescribeState(_subscribedManager)}");

        private void OnServerStopped(bool wasHost) => Write("NET", $"Servidor parado (era host={wasHost}).");

        private void OnClientStarted() => Write("NET", $"Cliente iniciado, tentando conectar. {DescribeState(_subscribedManager)}");

        private void OnClientStopped(bool wasHost) =>
            Write("NET", $"Cliente parado (era host={wasHost}). Motivo: {Reason(_subscribedManager)}");

        private void OnClientConnected(ulong clientId)
        {
            NetworkManager nm = _subscribedManager;
            int count = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : -1;
            Write("NET", $"Cliente {clientId} conectado (local={nm?.LocalClientId}, total no servidor={count}).");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            NetworkManager nm = _subscribedManager;
            Write("NET",
                $"Cliente {clientId} desconectado (local={nm?.LocalClientId}, transição proposital={NetworkSession.IsTransitioning}). " +
                $"Motivo: {Reason(nm)}. {DescribeState(nm)}");
        }

        private void OnTransportFailure()
        {
            Write("ERROR", $"FALHA DE TRANSPORTE. {DescribeState(_subscribedManager)} | Porta livre agora? {ProbeUdpPort(CurrentPort(_subscribedManager))}");
        }

        private void OnDestroy() => Unsubscribe();

        // ---------------------------------------------------------------- porta

        /// <summary>
        /// Antes de hospedar, tenta abrir a porta UDP configurada e solta na hora. Se outra coisa
        /// (outra instância do jogo, outro programa) estiver usando, fica registrado aqui.
        /// </summary>
        private void ProbePortIfChanged(NetworkManager nm)
        {
            int port = CurrentPort(nm);
            if (port <= 0 || port == _lastProbedPort) return;

            _lastProbedPort = port;
            Write("NET", $"Teste da porta UDP {port} antes de iniciar: {ProbeUdpPort(port)}");
        }

        private static int CurrentPort(NetworkManager nm)
        {
            if (nm == null || !nm.TryGetComponent(out UnityTransport t)) return -1;
            return t.ConnectionData.Port;
        }

        private static string ProbeUdpPort(int port)
        {
            if (port <= 0) return "porta desconhecida";

            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.ExclusiveAddressUse = true;
                    socket.Bind(new IPEndPoint(IPAddress.Any, port));
                }

                return "LIVRE";
            }
            catch (SocketException e)
            {
                return $"OCUPADA/BLOQUEADA ({e.SocketErrorCode}: {e.Message})";
            }
            catch (Exception e)
            {
                return $"não deu para testar ({e.Message})";
            }
        }

        // ---------------------------------------------------------------- descrições

        private static string DescribeTransport(NetworkManager nm)
        {
            if (!nm.TryGetComponent(out UnityTransport t)) return $"transporte={nm.NetworkConfig?.NetworkTransport?.GetType().Name ?? "nenhum"}";

            UnityTransport.ConnectionAddressData d = t.ConnectionData;
            return $"protocolo={t.Protocol}, endereço={d.Address}:{d.Port}, escuta={d.ServerListenAddress ?? "(padrão)"}, " +
                   $"modo={MultiplayerModeLabel()}";
        }

        private static string DescribeState(NetworkManager nm)
        {
            if (nm == null) return "NetworkManager nulo";

            return $"[IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsHost={nm.IsHost}, IsClient={nm.IsClient}, " +
                   $"IsConnectedClient={nm.IsConnectedClient}, ShutdownInProgress={nm.ShutdownInProgress}, {DescribeTransport(nm)}]";
        }

        private static string Reason(NetworkManager nm)
        {
            string r = nm?.DisconnectReason;
            return string.IsNullOrWhiteSpace(r) ? "(vazio)" : r;
        }

        private static string MultiplayerModeLabel() => Components.MultiplayerModeManager.CurrentMode.ToString();

        private static string DescribeLocalInterfaces()
        {
            try
            {
                var parts = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(n =>
                    {
                        string ips = string.Join(", ", n.GetIPProperties().UnicastAddresses
                            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                            .Select(a => a.Address.ToString()));
                        return string.IsNullOrEmpty(ips) ? null : $"{n.Name} ({n.NetworkInterfaceType}) = {ips}";
                    })
                    .Where(s => s != null);

                string result = string.Join(" | ", parts);
                return string.IsNullOrEmpty(result) ? "nenhuma interface IPv4 ativa" : result;
            }
            catch (Exception e)
            {
                return $"não deu para listar ({e.Message})";
            }
        }
    }
}
