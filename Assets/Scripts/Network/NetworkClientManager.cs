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
            // A session we tore down on purpose to start another one also raises this callback.
            // Reacting to it would load the main menu on top of the session being started.
            if (NetworkSession.IsTransitioning) return;

            if (clientId != 0 && clientId != _networkManager.LocalClientId) return;

            // The session is gone, and with it the match's lobby and voice channel. Staying in them
            // kept this player in the old voice channel on the menu, and kept the lobby marked as
            // started, so the next online match never began.
            try
            {
                VivoxManager.instance?.LeaveVoiceChannel();
                Lobby.instance?.LeaveLobby();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

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