using System;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace Network
{
    public static class ConnectionPayload
    {
        public const string DEFAULT_PLAYER_NAME = "Jogador";
        
        public static UserData BuildLocalUserData()
        {
            string playerName = PlayerPrefs.GetString(NameSelector.PLAYER_NAME_KEY, string.Empty);

            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = DEFAULT_PLAYER_NAME + UnityEngine.Random.Range(0, 999999);
            }

            return new UserData
            {
                playerName = playerName,
                userAuthId = ResolveAuthId()
            };
        }

        private static string ResolveAuthId()
        {
            try
            {
                if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                {
                    return AuthenticationService.Instance.PlayerId;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ConnectionPayload: Authentication unavailable, using a local id. {e.Message}");
            }

            return LocalFallbackId();
        }

        private const string LOCAL_ID_KEY = "LocalFallbackAuthId";

        private static string LocalFallbackId()
        {
            string id = PlayerPrefs.GetString(LOCAL_ID_KEY, string.Empty);

            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(LOCAL_ID_KEY, id);
                PlayerPrefs.Save();
            }

            return id;
        }
        
        public static void ApplyTo(NetworkManager networkManager)
        {
            if (networkManager == null) return;

            string json = JsonUtility.ToJson(BuildLocalUserData());
            networkManager.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(json);
        }
        
        public static UserData Parse(byte[] payload)
        {
            UserData userData = null;

            if (payload != null && payload.Length > 0)
            {
                try
                {
                    string json = System.Text.Encoding.UTF8.GetString(payload);
                    userData = JsonUtility.FromJson<UserData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"ConnectionPayload: Could not parse approval payload. {e.Message}");
                }
            }

            userData ??= new UserData();

            if (string.IsNullOrWhiteSpace(userData.playerName))
            {
                userData.playerName = DEFAULT_PLAYER_NAME;
            }

            if (string.IsNullOrWhiteSpace(userData.userAuthId))
            {
                userData.userAuthId = Guid.NewGuid().ToString();
            }

            return userData;
        }
    }
}
