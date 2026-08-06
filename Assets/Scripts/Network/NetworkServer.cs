using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class NetworkServer : IDisposable
    {
        private NetworkManager _networkManager;
        
        private Dictionary<ulong, string> clientIdToAuth = new Dictionary<ulong, string>();
        private Dictionary<string, UserData> authIdToUserData = new Dictionary<string, UserData>();

        public NetworkServer(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            
            _networkManager.ConnectionApprovalCallback += NetworkManager_ApprovalCheck;
            _networkManager.OnServerStarted += NetworkManager_OnServerStarted;
        }

        private void NetworkManager_ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
            UserData userData = JsonUtility.FromJson<UserData>(payload);
            
            clientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
            authIdToUserData[userData.userAuthId] = userData;
            
            response.Approved = true;
        }
        
        private void NetworkManager_OnServerStarted()
        {
            _networkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnected;
        }

        private void NetworkManager_OnClientDisconnected(ulong clientId)
        {
            if (clientIdToAuth.TryGetValue(clientId, out string authId))
            {
                authIdToUserData.Remove(authId);
                clientIdToAuth.Remove(clientId);
            }
        }

        public void Dispose()
        {
            if(_networkManager == null) return;
            
            _networkManager.ConnectionApprovalCallback -= NetworkManager_ApprovalCheck;
            _networkManager.OnClientDisconnectCallback -= NetworkManager_OnClientDisconnected;
            _networkManager.OnServerStarted -= NetworkManager_OnServerStarted;
            
            if(_networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }
            
        }
    }
}

