using System;
using Systems;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class NetworkClientManager : IDisposable
    {
        private NetworkManager _networkManager;

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
            if (_networkManager != null)
            {
                _networkManager.OnClientDisconnectCallback -= NetworkManager_OnClientDisconnect;
            }
        }
    }
}