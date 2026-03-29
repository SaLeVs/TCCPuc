using System;
using System.Threading.Tasks;
using Systems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Network
{
    public class ClientGameManager
    {
        private const int MAX_TRIES_TO_AUTH = 5;
        
        private JoinAllocation _allocation;
        
        
        public async Task<bool> InitAsync()
        {
            await UnityServices.InitializeAsync();
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
                RelayServerData relayData = new RelayServerData(
                    _allocation.RelayServer.IpV4,
                    (ushort)_allocation.RelayServer.Port,
                    _allocation.AllocationIdBytes,
                    _allocation.Key,
                    _allocation.ConnectionData,
                    _allocation.ConnectionData,
                    true
                );
                
                transport.SetRelayServerData(relayData);
                
                NetworkManager.Singleton.StartClient();
            }
            
            
        }
        
    }
}

