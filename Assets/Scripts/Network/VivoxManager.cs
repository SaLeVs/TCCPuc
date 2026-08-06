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

        private void Start()
        {
            LoginUserAsync();
        }
        
        private async void LoginUserAsync()
        {
            var loginOptions = new LoginOptions()
            {
                DisplayName = "TestPlayer",
                EnableTTS = true 
            };
            
            await VivoxService.Instance.LoginAsync(loginOptions);
            VivoxInputDevice myMic = VivoxService.Instance.AvailableInputDevices[0];
            await VivoxService.Instance.SetActiveInputDeviceAsync(myMic);

            VivoxOutputDevice mySpeakers = VivoxService.Instance.AvailableOutputDevices[0];
            await VivoxService.Instance.SetActiveOutputDeviceAsync(mySpeakers);
        }

        private void OnEnable()
        {
            VivoxService.Instance.ChannelJoined += OnChannelJoined;
            VivoxService.Instance.ChannelLeft += OnChannelLeft;
        }

        private void OnChannelJoined(string channelName)
        {
            
            Debug.Log($"Joined channel: {channelName}");
        }

        private void OnChannelLeft(string channelName)
        {
            Debug.Log($"Left channel: {channelName}");
        }

        public async void JoinChannelAsync()
        {
            await VivoxService.Instance.JoinEchoChannelAsync("TestPlayer", ChatCapability.AudioOnly);
        }
    }
}

