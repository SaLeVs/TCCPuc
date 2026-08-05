using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class NetworkServer
    {
        private NetworkManager _networkManager;

        public NetworkServer(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _networkManager.ConnectionApprovalCallback += NetworkManager_ApprovalCheck;
        }

        private void NetworkManager_ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
            UserData userData = JsonUtility.FromJson<UserData>(payload);

            response.Approved = true;
        }
    }
}

