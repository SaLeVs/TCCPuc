using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network
{
    public class NetworkServer : IDisposable
    {
        // Same cap as the online lobby (Lobby.MAX_PLAYERS), host included.
        private const int MaxPlayers = 4;

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
            response.CreatePlayerObject = false;

            // The host's own client is always let in; the checks below are for the others. Online, the
            // lobby and the relay already cap the session at four and close it once it starts; LAN had
            // neither, so a fifth player, or someone joining a match already under way, got in and was
            // left out of it - no missions, nobody waiting for them.
            if (request.ClientNetworkId != NetworkManager.ServerClientId)
            {
                if (_networkManager.ConnectedClientsIds.Count >= MaxPlayers)
                {
                    Refuse(response, "A partida está cheia (máximo de 4 jogadores).");
                    return;
                }

                if (IsMatchUnderway(SceneManager.GetActiveScene().name))
                {
                    Refuse(response, "Essa partida já começou. Espere ela acabar ou peça para criarem outra.");
                    return;
                }
            }

            UserData userData = ConnectionPayload.Parse(request.Payload);

            _clientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
            _authIdToUserData[userData.userAuthId] = userData;

            response.Approved = true;
        }

        private static void Refuse(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.Reason = reason;
        }

        /// <summary>
        /// The match scene (Game, or a Game1..7 prototype level) or its Win/Lose ending. The tutorial
        /// (Game0_Tutorial) and the GYM take players at any time, as before.
        /// </summary>
        private static bool IsMatchUnderway(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || sceneName.Contains("Tutorial")) return false;
            if (sceneName == "Win" || sceneName == "Lose" || sceneName == "Game") return true;

            return sceneName.Length > 4 && sceneName.StartsWith("Game") && char.IsDigit(sceneName[4]);
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

