using System;
using System.Threading.Tasks;
using Components;
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
            // Clear any leftover session first — a previous LAN/online attempt that is still
            // listening makes StartClient() return false.
            await NetworkSession.EnsureStoppedAsync();

            MultiplayerModeManager.SetOnline();

            try
            {
                _allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            }
            catch (Exception e)
            {
                Debug.Log(e);
                ConnectionFeedback.Report("Código de sala inválido ou serviço indisponível.");
                return;
            }

            if (NetworkManager.Singleton.TryGetComponent(out UnityTransport transport))
            {
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(_allocation, "dtls"));

            }

            ConnectionPayload.ApplyTo(NetworkManager.Singleton);

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError($"ClientGameManager: StartClient failed. {NetworkSession.DescribeState()}");
                ConnectionFeedback.Report("Não foi possível iniciar a conexão. Feche e reabra a sessão e tente de novo.");
            }
        }

        public async Task<bool> StartLanClientAsync()
        {
            await NetworkSession.EnsureStoppedAsync();

            ConnectionPayload.ApplyTo(NetworkManager.Singleton);

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError($"ClientGameManager: StartLanClient failed. {NetworkSession.DescribeState()}");
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

