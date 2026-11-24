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

        // How long the boot waits on the services before opening the menu without them.
        private const int SERVICES_TIMEOUT_MS = 8000;
        private const int AUTH_TIMEOUT_MS = 12000;
        
        private JoinAllocation _allocation;
        
        private NetworkClientManager _networkClientManager;
        
        
        public async Task<bool> InitAsync()
        {
            // Needs no service, and has to exist offline too: it is what takes a client back to the
            // menu when a LAN session drops.
            _networkClientManager = new NetworkClientManager(NetworkManager.Singleton);

            // A firewall that drops packets instead of refusing them leaves these calls retrying for a
            // long time, and the menu waited on them behind a black screen. Each gets a hard limit.
            try
            {
                Task init = UnityServices.InitializeAsync();

                if (await Task.WhenAny(init, Task.Delay(SERVICES_TIMEOUT_MS)) != init)
                {
                    Debug.LogWarning("ClientGameManager: Unity Services did not answer in time. Online play is off, LAN still works.");
                    return false;
                }

                await init;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ClientGameManager: Unity Services are unavailable. Online play is off, LAN still works. {e.Message}");
                return false;
            }

            Task<AuthenticationState> auth = AuthenticationController.Authenticate(MAX_TRIES_TO_AUTH);

            if (await Task.WhenAny(auth, Task.Delay(AUTH_TIMEOUT_MS)) != auth)
            {
                AuthenticationController.MarkTimedOut();
                Debug.LogWarning("ClientGameManager: signing in did not finish in time. Online play is off, LAN still works.");
                return false;
            }

            if (await auth == AuthenticationState.Authenticated)
            {
                return true;
            }

            Debug.LogWarning("ClientGameManager: could not sign in. Online play is off, LAN still works.");
            return false;
        }

        public void StartMenu()
        {
            // The first time on each microphone the mic setup comes first: every mic arrives at its own
            // level, and a hot one deafened the others from the first word. It moves on to the menu.
            Loader.Load(MicSettings.NeedsSetup ? Loader.Scene.MicSetup : Loader.Scene.MainMenu);
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
                Debug.LogException(e);
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

