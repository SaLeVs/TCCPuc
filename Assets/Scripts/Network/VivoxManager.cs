using System;
using UnityEngine;
using Unity.Services.Vivox;

namespace Network
{
    public class VivoxManager : MonoBehaviour
    {
        private static VivoxManager Instance;
        
        public static VivoxManager instance
        {
            get
            {
                if (Instance != null)
                {
                    return Instance;
                }
                
                Instance = FindFirstObjectByType<VivoxManager>();

                if (Instance == null)
                {
                    Debug.LogError("VivoxManager not found");
                    return null;
                }
                return Instance;
            }
        }
        
        [SerializeField] private Lobby lobbyManager;
        
        private string _currentChannelName;

        
        private void Start()
        {
            VivoxService.Instance.LoggedIn += VivoxService_OnUserLoggedIn;
            VivoxService.Instance.LoggedOut += VivoxService_OnUserLoggedOut;
            
            VivoxService.Instance.ChannelJoined += VivoxService_OnChannelJoined;
            VivoxService.Instance.ChannelLeft += VivoxService_OnChannelLeft;

            lobbyManager.OnJoinedLobby += VivoxService_OnJoinedLobby;
            lobbyManager.OnLeftLobby += VivoxService_OnLeftLobby;
        }
        

        private void VivoxService_OnJoinedLobby()
        {
            JoinVoiceForCurrentLobby();
        }

        private void VivoxService_OnLeftLobby()
        {
            LeaveVoiceChannel();
        }

        private async void JoinVoiceForCurrentLobby()
        {
            try
            {
                if (lobbyManager.JoinedLobby == null) return;   

                LoginOptions loginOptions = new LoginOptions()
                {
                    DisplayName = LocalUserData.Load().playerName,
                    ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
                };

                if (!VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LoginAsync(loginOptions);
                }

                _currentChannelName = lobbyManager.JoinedLobby.Id;  
                await VivoxService.Instance.JoinGroupChannelAsync(_currentChannelName, ChatCapability.TextAndAudio);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error joining voice channel: {e.Message}");
            }
        }

        public async void LeaveVoiceChannel()
        {
            if (string.IsNullOrEmpty(_currentChannelName)) return;
            await VivoxService.Instance.LeaveChannelAsync(_currentChannelName);
            _currentChannelName = null;
        }

        private void VivoxService_OnChannelJoined(string channelName)
        {
            Debug.Log($"Joined channel: {channelName}");
        }

        private void VivoxService_OnChannelLeft(string channelName)
        {
            Debug.Log($"Left channel: {channelName}");
        }

        private void VivoxService_OnUserLoggedIn()
        {
            Debug.Log("User logged in");
        }

        private void VivoxService_OnUserLoggedOut()
        {
            Debug.Log("User logged out");
        }
        
    }
}

