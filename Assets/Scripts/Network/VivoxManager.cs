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

        public async void JoinChannelAsync()
        {
            Debug.Log("JoinChannelAsync");
            await VivoxService.Instance.JoinEchoChannelAsync("ChannelName", ChatCapability.AudioOnly);
        }
    }
}

