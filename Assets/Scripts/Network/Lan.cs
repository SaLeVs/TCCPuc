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
            if (!ushort.TryParse(_port, out ushort portNumber))
            {
                Debug.LogError("Port number is not valid!");
                return;
            }

            MultiplayerModeManager.SetLan();

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", portNumber);

            Debug.Log($"Set connection data: port - {portNumber}, ip - {_ip}");
            HostSingleton.instance.gameManager.StartLanHost();
        }

        public void JoinSessionLan()
        {
            if (!ushort.TryParse(_port, out ushort portNumber))
            {
                Debug.LogError("Port number is not valid!");
                return;
            }

            MultiplayerModeManager.SetLan();

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(_ip, portNumber);

            Debug.Log($"Set connection data: port - {portNumber}, ip - {_ip}");

            ClientSingleton.instance.gameManager.StartLanClient();
        }
    }
}