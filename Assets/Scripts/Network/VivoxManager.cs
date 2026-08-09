using System;
using System.Threading.Tasks;
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
                if (Instance != null) return Instance;
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
        [SerializeField] private int audibleDistance;
        [SerializeField] private int conversationalDistance;
        [SerializeField] private float audioFadeIntensity;
        [SerializeField] private AudioFadeModel audioFadeModel;

        
        public string CurrentChannelName => _currentChannelName;
        public bool IsInPositionalChannel { get; private set; }
        
        private const string ECHO_CHANNEL_NAME = "MicTestChannel";
        private const string LOBBY_CHANNEL_SUFFIX = "_lobby";
        private const string GAME_CHANNEL_SUFFIX = "_game";

        private string _currentChannelName;
        private ChatCapability _currentChannelCapability;
        private string _channelBeforeTest;
        private ChatCapability _channelBeforeTestCapability;
        private bool _isInTestChannel;
        private bool _isSwitchingChannel;

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
            EnterLobbyVoice();
        }

        private void VivoxService_OnLeftLobby()
        {
            LeaveVoiceChannel();
        }

        private async Task EnsureLoggedInAsync()
        {
            if (VivoxService.Instance.IsLoggedIn) return;

            LoginOptions loginOptions = new LoginOptions()
            {
                DisplayName = LocalUserData.Load().playerName,
                ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
            };

            await VivoxService.Instance.LoginAsync(loginOptions);
        }

        public async void EnterLobbyVoice()
        {
            if (lobbyManager.JoinedLobby == null) return;
            await SwitchToGroupChannelAsync(lobbyManager.JoinedLobby.Id + LOBBY_CHANNEL_SUFFIX, ChatCapability.TextAndAudio);
        }

        public async void EnterGameVoice()
        {
            if (lobbyManager.JoinedLobby == null) return;
            await SwitchToGroupChannelAsync(lobbyManager.JoinedLobby.Id + GAME_CHANNEL_SUFFIX, ChatCapability.AudioOnly);
        }

        
        private async Task SwitchToGroupChannelAsync(string newChannelName, ChatCapability capability)
        {
            if (_isSwitchingChannel) return;
            if (_currentChannelName == newChannelName) return;
            if (_isInTestChannel) return;

            _isSwitchingChannel = true;
            IsInPositionalChannel = false;

            try
            {
                await EnsureLoggedInAsync();

                if (!string.IsNullOrEmpty(_currentChannelName))
                {
                    await VivoxService.Instance.LeaveChannelAsync(_currentChannelName);
                    _currentChannelName = null;
                }

                await VivoxService.Instance.JoinGroupChannelAsync(newChannelName, capability);

                _currentChannelName = newChannelName;
                _currentChannelCapability = capability;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error switching to group voice channel: {e.Message}");
            }
            finally
            {
                _isSwitchingChannel = false;
            }
        }

        public async void LeaveVoiceChannel()
        {
            if (string.IsNullOrEmpty(_currentChannelName)) return;
            IsInPositionalChannel = false;
            await VivoxService.Instance.LeaveChannelAsync(_currentChannelName);
            _currentChannelName = null;
        }

        public async void EnterTestVoiceChannel()
        {
            try
            {
                if (_isInTestChannel) return;

                await EnsureLoggedInAsync();

                if (!string.IsNullOrEmpty(_currentChannelName))
                {
                    _channelBeforeTest = _currentChannelName;
                    _channelBeforeTestCapability = _currentChannelCapability;
                    await VivoxService.Instance.LeaveChannelAsync(_currentChannelName);
                    _currentChannelName = null;
                }

                await VivoxService.Instance.JoinEchoChannelAsync(ECHO_CHANNEL_NAME, ChatCapability.AudioOnly);
                _isInTestChannel = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error entering test voice channel: {e.Message}");
            }
        }

        public async void LeaveTestVoiceChannel()
        {
            try
            {
                if (!_isInTestChannel) return;

                await VivoxService.Instance.LeaveChannelAsync(ECHO_CHANNEL_NAME);
                _isInTestChannel = false;

                if (!string.IsNullOrEmpty(_channelBeforeTest))
                {
                    string channelToRejoin = _channelBeforeTest;
                    ChatCapability capabilityToRejoin = _channelBeforeTestCapability;
                    _channelBeforeTest = null;

                    Channel3DProperties properties = new Channel3DProperties(audibleDistance: audibleDistance, conversationalDistance: conversationalDistance, audioFadeIntensityByDistanceaudio: audioFadeIntensity, audioFadeModel: audioFadeModel);

                    await VivoxService.Instance.JoinPositionalChannelAsync(channelToRejoin, capabilityToRejoin, properties);

                    _currentChannelName = channelToRejoin;
                    _currentChannelCapability = capabilityToRejoin;
                    IsInPositionalChannel = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error leaving test voice channel: {e.Message}");
            }
        }

        private void VivoxService_OnChannelJoined(string channelName) => Debug.Log($"Joined channel: {channelName}");
        private void VivoxService_OnChannelLeft(string channelName) => Debug.Log($"Left channel: {channelName}");
        private void VivoxService_OnUserLoggedIn() => Debug.Log("User logged in");
        private void VivoxService_OnUserLoggedOut() => Debug.Log("User logged out");
    }
}