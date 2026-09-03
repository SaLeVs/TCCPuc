using System;
using System.Threading.Tasks;
using Systems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Network
{
    public class ClientGameManager : IDisposable
    {
        private const int MAX_TRIES_TO_AUTH = 5;
        
        private JoinAllocation _allocation;
        
        private NetworkClientManager _networkClientManager;
        
        
        public async Task<bool> InitAsync()
        {
            await UnityServices.InitializeAsync();
            
            _networkClientManager = new NetworkClientManager(NetworkManager.Singleton);
            
            AuthenticationState authState = await AuthenticationController.Authenticate(MAX_TRIES_TO_AUTH);
            
            if (authState == AuthenticationState.Authenticated)
            {
                Debug.Log("ClientGameManager: Authenticated");
                return true;
            }
            
            Debug.Log("ClientGameManager: Failed to initialize");
            return false;
        }

        public void StartMenu()
        {
            Loader.Load(Loader.Scene.MainMenu);
        }

        public async Task StartClientAsync(string joinCode)
        {
            try
            { 
                _allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            
            if (NetworkManager.Singleton.TryGetComponent(out UnityTransport transport))
            {
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(_allocation, "dtls"));
                
            }
            
            ConnectionPayload.ApplyTo(NetworkManager.Singleton);

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("ClientGameManager: StartClient failed.");
            }
        }

        public bool StartLanClient()
        {
            ConnectionPayload.ApplyTo(NetworkManager.Singleton);

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("ClientGameManager: StartLanClient failed — check the IP, the port and the firewall.");
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            _networkClientManager?.Dispose();
        }
    }
}

