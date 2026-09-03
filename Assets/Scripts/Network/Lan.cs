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
        private string _port;

        public void ValidateIP(string text)
        {
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

            if (filtered != text)
                ipInput.text = filtered;
        }

        public void OnChangeIpInputField(string ip)
        {
            _ip = ip;
            Debug.Log($"ip changed to {ip}");
        }

        public void OnChangePortInputField(string portCode)
        {
            _port = portCode;
            Debug.Log($"port changed to {_port}");
        }

        public void StartSessionLan()
        {
            if (!TryReadPort(out ushort portNumber)) return;

            MultiplayerModeManager.SetLan();

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            transport.SetConnectionData("0.0.0.0", portNumber, "0.0.0.0");

            Debug.Log($"Lan: hosting on 0.0.0.0:{portNumber}");

            if (!HostSingleton.instance.gameManager.StartLanHost())
            {
                ConnectionFeedback.Report($"Not possible on port {portNumber}. It may be in use or blocked by the firewall.");
            }
        }

        public void JoinSessionLan()
        {
            if (!TryReadPort(out ushort portNumber)) return;

            if (!IsValidIPv4(_ip))
            {
                ConnectionFeedback.Report($"Invalid IP: '{_ip}'. Use the format 0.0.0.0 (e.g., 192.168.0.10).");
                return;
            }

            MultiplayerModeManager.SetLan();

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(_ip, portNumber);

            Debug.Log($"Lan: connecting to {_ip}:{portNumber}");

            if (!ClientSingleton.instance.gameManager.StartLanClient())
            {
                ConnectionFeedback.Report($"Not possible to start the connection with {_ip}:{portNumber}.");
            }
        }

        private bool TryReadPort(out ushort portNumber)
        {
            if (!ushort.TryParse(_port, out portNumber) || portNumber == 0)
            {
                ConnectionFeedback.Report($"Invalid port: '{_port}'. Use a number between 1 and 65535.");
                return false;
            }

            return true;
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