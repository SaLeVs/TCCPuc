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
            _networkManager.OnClientConnectedCallback += NetworkManager_OnClientConnected;
        }

        private void NetworkManager_OnClientConnected(ulong clientId)
        {
            if (clientId != _networkManager.LocalClientId) return;
            
            _networkManager.SceneManager.OnSceneEvent += NetworkManager_OnSceneEvent;
        }

        private void NetworkManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            Debug.Log($"Vivox: OnSceneEvent fired Type={sceneEvent.SceneEventType}, ClientId={sceneEvent.ClientId}, SceneName={sceneEvent.SceneName}");

            if (sceneEvent.SceneEventType != SceneEventType.LoadComplete)
            {
                Debug.Log($"Vivox: ignored: eventType is not load complete ({sceneEvent.SceneEventType})");
                return;
            }

            if (sceneEvent.SceneName != nameof(Loader.Scene.Lobby))
            {
                Debug.Log($"Vivox: ignored: scene '{sceneEvent.SceneName}' is not the 3D Lobby scene (expected: {nameof(Loader.Scene.Lobby)})");
                return;
            }

            Debug.Log("Vivox: All conditions ok, call EnterGameVoice()");
            VivoxManager.instance.EnterGameVoice();
        }

        private void NetworkManager_OnClientDisconnect(ulong clientId)
        {
            if (clientId != 0 && clientId != _networkManager.LocalClientId) return;

            if (_networkManager.SceneManager != null)
            {
                _networkManager.SceneManager.OnSceneEvent -= NetworkManager_OnSceneEvent;
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
                _networkManager.OnClientConnectedCallback -= NetworkManager_OnClientConnected;

                if (_networkManager.SceneManager != null)
                {
                    _networkManager.SceneManager.OnSceneEvent -= NetworkManager_OnSceneEvent;
                }
            }
        }
    }
}