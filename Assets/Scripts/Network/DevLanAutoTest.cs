#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Threading.Tasks;
using Components;
using Systems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network
{
    /// <summary>
    /// Só existe em build de desenvolvimento. Liga uma partida LAN sozinha a partir da linha de comando,
    /// para testar host e cliente na mesma máquina sem clicar em nada:
    ///
    ///   StreamMustGoOn.exe -lanhost 7790 -lanclients 1 -lanquit 90
    ///   StreamMustGoOn.exe -lanjoin 127.0.0.1 7790 -lanquit 90
    ///
    /// O host espera os clientes, carrega a Game pela rede e o LanDiagnosticsLog de cada cópia grava
    /// as fotos das portas. Sem esses argumentos não faz nada.
    /// </summary>
    public sealed class DevLanAutoTest : MonoBehaviour
    {
        private bool _host;
        private string _ip;
        private ushort _port;
        private int _clients = 1;
        private float _quitAfter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            string[] args = Environment.GetCommandLineArgs();
            var test = new GameObject("[DevLanAutoTest]").AddComponent<DevLanAutoTest>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-lanhost" when i + 1 < args.Length:
                        test._host = true;
                        ushort.TryParse(args[i + 1], out test._port);
                        break;
                    case "-lanjoin" when i + 2 < args.Length:
                        test._ip = args[i + 1];
                        ushort.TryParse(args[i + 2], out test._port);
                        break;
                    case "-lanclients" when i + 1 < args.Length:
                        int.TryParse(args[i + 1], out test._clients);
                        break;
                    case "-lanquit" when i + 1 < args.Length:
                        float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out test._quitAfter);
                        break;
                }
            }

            if (test._port == 0 || (!test._host && string.IsNullOrEmpty(test._ip)))
            {
                Destroy(test.gameObject);
                return;
            }

            DontDestroyOnLoad(test.gameObject);
        }

        private IEnumerator Start()
        {
            LanDiagnosticsLog.Note($"Teste automático: {(_host ? $"host na porta {_port}, esperando {_clients} cliente(s)" : $"cliente para {_ip}:{_port}")}");

            if (_quitAfter > 0f) StartCoroutine(QuitLater());

            // A primeira cena pede o nome; o teste pula direto para a rede.
            if (SceneManager.GetActiveScene().name == "Bootstrap")
            {
                SceneManager.LoadScene("Netbootstrap");
            }

            float deadline = Time.realtimeSinceStartup + 120f;
            while (!Ready() && Time.realtimeSinceStartup < deadline)
            {
                // A configuração do microfone vem antes do menu num PC novo; o PlayerTracker nasce no
                // MainMenu, então o teste precisa passar por lá.
                if (SceneManager.GetActiveScene().name == "MicSetup") SceneManager.LoadScene("MainMenu");
                yield return null;
            }

            if (!Ready())
            {
                LanDiagnosticsLog.Note("Teste automático: o jogo não chegou ao menu a tempo.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(2f);

            MultiplayerModeManager.SetLan();
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            if (_host)
            {
                transport.SetConnectionData("0.0.0.0", _port, "0.0.0.0");
                Task<bool> start = HostSingleton.instance.gameManager.StartLanHostAsync();
                while (!start.IsCompleted) yield return null;

                deadline = Time.realtimeSinceStartup + 120f;
                while (NetworkManager.Singleton.ConnectedClientsIds.Count < 1 + _clients && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                LanDiagnosticsLog.Note($"Teste automático: {NetworkManager.Singleton.ConnectedClientsIds.Count - 1} cliente(s) conectados; carregando a Game.");
                yield return new WaitForSecondsRealtime(3f);
                Loader.LoadNetwork(Loader.Scene.Game);
            }
            else
            {
                transport.SetConnectionData(_ip, _port);
                Task<bool> start = ClientSingleton.instance.gameManager.StartLanClientAsync();
                while (!start.IsCompleted) yield return null;
            }
        }

        private static bool Ready()
        {
            return SceneManager.GetActiveScene().name == "MainMenu"
                   && NetworkManager.Singleton != null
                   && HostSingleton.instance != null && HostSingleton.instance.gameManager != null
                   && ClientSingleton.instance != null && ClientSingleton.instance.gameManager != null;
        }

        private IEnumerator QuitLater()
        {
            yield return new WaitForSecondsRealtime(_quitAfter);
            LanDiagnosticsLog.Note("Teste automático: tempo esgotado, fechando.");
            Application.Quit();
        }
    }
}
#endif
