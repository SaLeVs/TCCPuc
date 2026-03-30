using System;
using System.Threading.Tasks;
using Systems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Network
{
    public class HostGameManager
    {
        private const int MAX_CONNECTIONS = 4;
        
        private Allocation _allocation;
        private string _joinCode;
        
        public async Task<string> StartHostAsync()
        {
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
            
            NetworkManager.Singleton.StartHost();
            Loader.LoadNetwork(Loader.Scene.Lobby);
            return _joinCode;
        }
        
    }
}

