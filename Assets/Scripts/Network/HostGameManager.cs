using System;
using System.Threading.Tasks;
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
        
        
        public async Task StartHostAsync()
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
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(_allocation.AllocationId);
                Debug.Log($"HostGameManager: Join code: {joinCode}");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
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
            }
            
            NetworkManager.Singleton.StartHost();
            
        }
        
    }
}

