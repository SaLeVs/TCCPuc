using System.Threading.Tasks;
using Components;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Network
{
    public class Lan : MonoBehaviour
    {
        [SerializeField] private TMP_InputField ipInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private ushort port;

        private string _ip;
        private string _hostPort;
        private string _joinPort;

        /// <summary>The "ip:port" typed in the join fields, for messages.</summary>
        public string JoinAddress => $"{_ip?.Trim()}:{_joinPort?.Trim()}";

        public void ValidateIP(string text)
        {
            string filtered = FilterIp(text);

            if (filtered != text)
                ipInput.text = filtered;
        }

        public void OnChangeIpInputField(string ip)
        {
            // Filtered here too: ValidateIP rewrites the field from inside the same change event, and
            // this listener still receives the unfiltered text after that.
            _ip = FilterIp(ip);
        }

        /// <summary>Sets both ports. Kept for anything still wired to it; the menu fields use the two below.</summary>
        public void OnChangePortInputField(string portCode)
        {
            _hostPort = portCode;
            _joinPort = portCode;
        }

        /// <summary>Port typed in the create field. Separate from the join port, so one field cannot overwrite the other.</summary>
        public void OnChangeHostPortInputField(string portCode)
        {
            _hostPort = portCode;
        }

        public void OnChangeJoinPortInputField(string portCode)
        {
            _joinPort = portCode;
        }

        /// <summary>Checks the create port before anything starts. Reports what is wrong and returns false.</summary>
        public bool ValidateHost()
        {
            return TryReadPort(_hostPort, out _);
        }

        /// <summary>Checks the join port and IP before anything starts. Reports what is wrong and returns false.</summary>
        public bool ValidateJoin()
        {
            return TryReadPort(_joinPort, out _) && TryReadIp(out _);
        }

        public async void StartSessionLan()
        {
            await StartSessionLanAsync();
        }

        public async void JoinSessionLan()
        {
            await JoinSessionLanAsync();
        }

        /// <summary>Hosts a LAN session. False when it did not start; the reason is already reported.</summary>
        public async Task<bool> StartSessionLanAsync()
        {
            if (!TryReadPort(_hostPort, out ushort portNumber)) return false;

            MultiplayerModeManager.SetLan();

            // Tear down any leftover session BEFORE writing the transport config, so the
            // shutdown can't race with the values we're about to set.
            await NetworkSession.EnsureStoppedAsync();

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // "0.0.0.0" as listen address = accept on every interface (Wi-Fi, Ethernet, Radmin VPN).
            transport.SetConnectionData("0.0.0.0", portNumber, "0.0.0.0");


            if (!await HostSingleton.instance.gameManager.StartLanHostAsync())
            {
                ConnectionFeedback.Report($"Não foi possível hospedar na porta {portNumber}. Ela pode estar em uso ou bloqueada pelo firewall.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Starts connecting to a LAN host. True only means the attempt is under way: the host moves
        /// us into its Lobby scene when it answers, and ConnectionFeedback reports a refusal or a timeout.
        /// </summary>
        public async Task<bool> JoinSessionLanAsync()
        {
            if (!TryReadPort(_joinPort, out ushort portNumber)) return false;
            if (!TryReadIp(out string ip)) return false;

            MultiplayerModeManager.SetLan();

            await NetworkSession.EnsureStoppedAsync();

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(ip, portNumber);


            if (!await ClientSingleton.instance.gameManager.StartLanClientAsync())
            {
                ConnectionFeedback.Report($"Não foi possível conectar a {ip}:{portNumber}. Verifique o IP, a porta e o firewall do host.");
                return false;
            }

            return true;
        }

        private static bool TryReadPort(string text, out ushort portNumber)
        {
            portNumber = 0;
            string value = text?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                ConnectionFeedback.Report("Digite a porta da partida (ex.: 7777).");
                return false;
            }

            if (!ushort.TryParse(value, out portNumber) || portNumber == 0)
            {
                ConnectionFeedback.Report($"Porta inválida: '{value}'. Use um número entre 1 e 65535.");
                return false;
            }

            return true;
        }

        private bool TryReadIp(out string ip)
        {
            ip = _ip?.Trim();

            if (string.IsNullOrEmpty(ip))
            {
                ConnectionFeedback.Report("Digite o IP de quem criou a partida (ex.: 192.168.0.10).");
                return false;
            }

            if (!IsValidIPv4(ip))
            {
                ConnectionFeedback.Report($"IP inválido: '{ip}'. Use o formato 0.0.0.0 (ex.: 192.168.0.10).");
                return false;
            }

            return true;
        }

        private static string FilterIp(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string filtered = "";

            foreach (char c in text)
            {
                if (char.IsDigit(c))
                {
                    filtered += c;
                }
                else if (c == '.')
                {
                    filtered += c;
                }
            }

            return filtered;
        }

        private static bool IsValidIPv4(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            string[] parts = value.Split('.');
            if (parts.Length != 4) return false;

            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 3) return false;
                if (!byte.TryParse(part, out _)) return false;
            }

            return true;
        }
    }
}
