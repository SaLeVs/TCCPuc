using System;
using System.Threading.Tasks;
using Systems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Network
{
    public class HostGameManager : IDisposable
    {
        public NetworkServer NetworkServer { get; private set; }
        
        private const int MAX_CONNECTIONS = 4;
        
        private Allocation _allocation;
        private string _joinCode;
        
        public async Task<string> StartHostAsync()
        {
            if (NetworkServer != null)
            {
                NetworkServer.Dispose();
                NetworkServer = null;
            }
            
            try
            {
                _allocation = await RelayService.Instance.CreateAllocationAsync(MAX_CONNECTIONS);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            try
            {
                _joinCode = await RelayService.Instance.GetJoinCodeAsync(_allocation.AllocationId);
                Debug.Log($"HostGameManager: Join code: {_joinCode}");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            if (NetworkManager.Singleton.TryGetComponent(out UnityTransport transport))
            {
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(_allocation, "dtls"));
            }
            
            NetworkServer = new NetworkServer(NetworkManager.Singleton);

            UserData userData = new UserData
            {
                playerName = PlayerPrefs.GetString(NameSelector.PLAYER_NAME_KEY, "Error"),
                userAuthId = AuthenticationService.Instance.PlayerId
            };
            
            string payload = JsonUtility.ToJson(userData);
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
            
            NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
            
            NetworkManager.Singleton.StartHost();
            Loader.LoadNetwork(Loader.Scene.Lobby);
            
            return _joinCode;
        }
        
        public void StartLanHost()
        {
            NetworkManager.Singleton.StartHost();
            Loader.LoadNetwork(Loader.Scene.Lobby);
        }

        public void Dispose()
        {
            NetworkServer?.Dispose();
            NetworkServer = null;

            _joinCode = null;
        }
        
    }
}

