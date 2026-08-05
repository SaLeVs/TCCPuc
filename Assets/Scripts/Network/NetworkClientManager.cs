using System;
using Systems;
using Unity.Netcode;

namespace Network
{
    public class NetworkClientManager : IDisposable
    {
        private NetworkManager _networkManager;
        private IDisposable _disposableImplementation;

        public NetworkClientManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            
            _networkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnect;
        }

        private void NetworkManager_OnClientDisconnect(ulong clientId)
        {
            if (clientId != 0 && clientId != _networkManager.LocalClientId) return;

            if (Loader.GetCurrentScene() != Loader.GetSceneByName(Loader.Scene.MainMenu))
            {
                Loader.Load(Loader.Scene.MainMenu);
            }

            if (_networkManager.IsConnectedClient)
            {
                _networkManager.Shutdown();
            }
        }

        public void Dispose()
        {
            
        }
    }
}

