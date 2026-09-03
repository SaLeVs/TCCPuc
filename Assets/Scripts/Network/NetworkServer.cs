using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class NetworkServer : IDisposable
    {
        private NetworkManager _networkManager;
        
        private Dictionary<ulong, string> _clientIdToAuth = new Dictionary<ulong, string>();
        private Dictionary<string, UserData> _authIdToUserData = new Dictionary<string, UserData>();

        public NetworkServer(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            
            _networkManager.ConnectionApprovalCallback += NetworkManager_ApprovalCheck;
            _networkManager.OnServerStarted += NetworkManager_OnServerStarted;
        }

        private void NetworkManager_ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            UserData userData = ConnectionPayload.Parse(request.Payload);

            _clientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
            _authIdToUserData[userData.userAuthId] = userData;

            response.Approved = true;
            response.CreatePlayerObject = false;

            Debug.Log($"NetworkServer: Approved client {request.ClientNetworkId} as '{userData.playerName}'");
        }
        
        private void NetworkManager_OnServerStarted()
        {
            _networkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnected;
        }

        private void NetworkManager_OnClientDisconnected(ulong clientId)
        {
            if (_clientIdToAuth.TryGetValue(clientId, out string authId))
            {
                _authIdToUserData.Remove(authId);
                _clientIdToAuth.Remove(clientId);
            }
        }

        public UserData GetUserDataByClient(ulong clientId)
        {
            if(_clientIdToAuth.TryGetValue(clientId, out string authId))
            {
                if(_authIdToUserData.TryGetValue(authId, out UserData userData))
                {
                    return userData;
                }
            }
            return null;
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

