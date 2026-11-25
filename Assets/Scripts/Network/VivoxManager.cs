using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using Unity.Services.Vivox;
using Components;

namespace Network
{
    public class VivoxManager : MonoBehaviour
    {
        public event Action<VivoxParticipant> OnParticipantJoinedChannel;
        public event Action<VivoxParticipant> OnParticipantLeftChannel;

        /// <summary>
        /// This machine left a channel. Vivox cleans that channel's participants up without raising
        /// OnParticipantLeftChannel for them, and any property read on one of those objects afterwards
        /// throws — so whoever kept one has to drop it here, by channel name.
        /// </summary>
        public event Action<string> OnLeftChannel;

        private static VivoxManager Instance;
        private static bool _reportedMissing;
        private static float _nextSearchTime;

        public static VivoxManager instance
        {
            get
            {
                if (Instance != null) return Instance;

                // Asked every frame by the voice components; with none in the scene (a scene opened
                // straight in the Editor) the search and the error used to run every frame too; now once a second.
                if (Time.unscaledTime < _nextSearchTime) return null;

                Instance = FindFirstObjectByType<VivoxManager>();
                if (Instance != null) return Instance;

                _nextSearchTime = Time.unscaledTime + 1f;

                if (!_reportedMissing)
                {
                    _reportedMissing = true;
                    Debug.LogWarning("VivoxManager not found: voice chat is off in this scene.");
                }

                return null;
            }
        }

        [SerializeField] private Lobby lobbyManager;

        [Tooltip("How far a shout (and a donation being read) carries, in meters. RemoteVoiceFilter fades " +
                 "every voice to silence by here; normal talking stops at the VoiceProfile's Speech Range. " +
                 "This is the voice range, not the channel's.")]
        [SerializeField] private int audibleDistance;

        [Tooltip("The audible distance handed to Vivox. Vivox drops anyone past it from the participant " +
                 "list, so a player who walked off left the channel: gone from the pause roster, their " +
                 "volume and mute forgotten. Kept far beyond the voice range so everyone stays in; the " +
                 "distance fade is what silences them. The fade model ignores this value, so it changes " +
                 "nothing inside the voice range. Every client must use the same value to join.")]
        [SerializeField] private int channelAudibleDistance = 1000;

        [SerializeField] private int conversationalDistance;
        [SerializeField] private float audioFadeIntensity;
        [SerializeField] private AudioFadeModel audioFadeModel;

        [Tooltip("Vivox's automatic gain control levels every mic to the same loudness, so a shout " +
                 "comes out about as loud as normal talking. Off keeps the level it was spoken at — " +
                 "which is what the voice range and the monster's hearing are calibrated against.")]
        [SerializeField] private bool automaticGainControl;

        public string CurrentChannelName => _currentChannelName;

        /// <summary>
        /// True only while the positional channel is actually joined — false while switching and
        /// before the join has finished, which is when Set3DPosition throws. The mic test no longer
        /// leaves it.
        /// </summary>
        public bool IsInPositionalChannel => _joinedChannel is { Positional: true }
                                             && !string.IsNullOrEmpty(_currentChannelName)
                                             && VivoxService.Instance.ActiveChannels.ContainsKey(_currentChannelName);

        /// <summary>The mic only reaches the echo channel while this is true.</summary>
        public bool IsInMicTest => _isInTestChannel;

        public int AudibleDistance => audibleDistance;
        public int ConversationalDistance => conversationalDistance;
        
        public IEnumerable<VivoxParticipant> CurrentParticipants
        {
            get
            {
                if (string.IsNullOrEmpty(_currentChannelName)) yield break;
                if (!VivoxService.Instance.ActiveChannels.TryGetValue(_currentChannelName, out ReadOnlyCollection<VivoxParticipant> participants)) yield break;

                foreach (VivoxParticipant participant in participants)
                {
                    yield return participant;
                }
            }
        }
        
        private const string ECHO_CHANNEL_NAME = "MicTestChannel";
        private const string LOBBY_CHANNEL_SUFFIX = "_lobby";
        private const string GAME_CHANNEL_SUFFIX = "_game";
        private const string END_CHANNEL_SUFFIX = "_end";
        private const string LAN_CHANNEL_PREFIX = "lan_";

        private bool _reportedNoServices;
        private string _lastLanSessionKey;
        private bool _leavingTestChannel;
        private bool _reloginBeforeJoin;

        private sealed class VoiceChannel
        {
            public readonly string Name;
            public readonly ChatCapability Capability;
            public readonly bool Positional;

            public VoiceChannel(string name, ChatCapability capability, bool positional)
            {
                Name = name;
                Capability = capability;
                Positional = positional;
            }
        }

        // What the game asked for and what Vivox is actually in. Requests only change the wanted
        // side; ReconcileChannelsAsync walks the joined side towards it one step at a time. A request
        // that arrives mid-switch or mid-test is never dropped — it just changes where the walk ends.
        // The mic test sits next to the lobby/game channel, never in its place.
        private VoiceChannel _wantedChannel;
        private bool _wantsTestChannel;

        private VoiceChannel _joinedChannel;
        private bool _isInTestChannel;
        private bool _isReconciling;

        // Consecutive failed walks. Retries back off from FirstRetrySeconds, doubling up to MaxRetrySeconds.
        private int _failedAttempts;
        private const float FirstRetrySeconds = 2f;
        private const float MaxRetrySeconds = 30f;
        private const int WarnAfterFailures = 3;

        private string _currentChannelName;

        // Per player, keyed by their Unity Authentication id — the same id Vivox uses, stable across
        // sessions as long as that player signs in on the same machine.
        private const string VOLUME_KEY_PREFIX = "VoiceVolume_";
        private const string MUTED_KEY_PREFIX = "VoiceMuted_";
        private const int MIN_PARTICIPANT_VOLUME = -50;
        private const int MAX_PARTICIPANT_VOLUME = 50;

        private readonly Dictionary<string, float> _tapVolumes = new();

        // What each speaker's own mic setup asked everyone to take off their voice, in dB (0 or less),
        // read from the lobby's player data by PlayerId.
        private readonly Dictionary<string, int> _speakerTrims = new();

        // How loud each remote voice is right now, measured from its tap by RemoteVoiceFilter: Vivox
        // reports AudioEnergy 0 and SpeechDetected false for every participant that has a tap.
        private readonly Dictionary<string, float> _heardEnergy = new();

        // Vivox's own output carries only what has no tap. In a match that is the donation readings
        // addressed to this player (plus the mic test's echo); everywhere else it is every voice.
        private bool _outputCarriesReadings;
        private int _playerOutputVolume;


        private void Start()
        {
            VivoxService.Instance.ParticipantAddedToChannel += VivoxService_OnParticipantAddedToChannel;
            VivoxService.Instance.ParticipantRemovedFromChannel += VivoxService_OnParticipantRemovedFromChannel;
            VivoxService.Instance.ChannelLeft += VivoxService_OnChannelLeft;
            VivoxService.Instance.EffectiveInputDeviceChanged += MicSettings.ApplyToCurrentDevice;

            Lobby.instance.OnJoinedLobby += VivoxService_OnJoinedLobby;
            Lobby.instance.OnLeftLobby += VivoxService_OnLeftLobby;
            Lobby.instance.OnLobbyUpdated += Lobby_OnLobbyUpdated;

            VoiceChatSettings.OnChanged += ApplyOutputVolume;

            Application.quitting += Application_ApplicationQuit;
        }

        private void VivoxService_OnJoinedLobby() => EnterLobbyVoice();
        private void VivoxService_OnLeftLobby() => LeaveVoiceChannel();

        private async Task EnsureLoggedInAsync()
        {
            // Vivox failed to start at sign-in (its servers unreachable then): a retry starts it now.
            if (!OnlineServices.IsVoiceAvailable)
            {
                await VivoxService.Instance.InitializeAsync();
                OnlineServices.IsVoiceAvailable = true;
            }

            ApplyGlobalAudioSettings();

            if (VivoxService.Instance.IsLoggedIn) return;

            LoginOptions loginOptions = new LoginOptions()
            {
                DisplayName = LocalUserData.Load().playerName,
                ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
            };

            await VivoxService.Instance.LoginAsync(loginOptions);

            // MicSettings skips the level while logged out, so it goes on now.
            MicSettings.ApplyToCurrentDevice();
        }

        /// <summary>
        /// Global to the Vivox client, so it is set before every channel this machine joins — the
        /// echo test included, where the player calibrates against the same signal the game hears.
        /// </summary>
        private void ApplyGlobalAudioSettings()
        {
            try
            {
                IVivoxGlobalAudioSettings settings = VivoxService.Instance.VivoxGlobalAudioSettings;

                if (settings.AutomaticGainControlEnabled != automaticGainControl)
                {
                    settings.AutomaticGainControlEnabled = automaticGainControl;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not set Vivox automatic gain control: {e.Message}");
            }

            // With the gain control off, the level this player set for their mic is what keeps it
            // from going out as loud as the hardware makes it.
            MicSettings.ApplyToCurrentDevice();
        }

        public void EnterLobbyVoice()
        {
            if (lobbyManager.JoinedLobby == null) return;

            _wantedChannel = new VoiceChannel(lobbyManager.JoinedLobby.Id + LOBBY_CHANNEL_SUFFIX, ChatCapability.TextAndAudio, positional: false);
            ReconcileChannels();
        }

        /// <param name="lanSessionKey">The match's key from the host (PlayerVoiceIdentity), used when
        /// there is no Unity lobby to name the channel after - a LAN match. It used to have no voice at all.</param>
        public void EnterGameVoice(string lanSessionKey = null)
        {
            string channelBase = lobbyManager != null && lobbyManager.JoinedLobby != null
                ? lobbyManager.JoinedLobby.Id
                : string.IsNullOrEmpty(lanSessionKey) ? null : LAN_CHANNEL_PREFIX + lanSessionKey;

            if (channelBase == null) return;

            if (!string.IsNullOrEmpty(lanSessionKey)) _lastLanSessionKey = lanSessionKey;

            // Signed out of the Unity services (a network that blocks them): Vivox cannot log in,
            // and retrying would only fail every thirty seconds for the whole match.
            if (!OnlineServices.IsAvailable)
            {
                if (!_reportedNoServices)
                {
                    _reportedNoServices = true;
                    LanDiagnosticsLog.Note("VOZ: sem login nos serviços da Unity nesta rede; partida sem chat de voz.");
                    ScreenToast.Show("Chat de voz indisponível: esta rede bloqueia os serviços da Unity.", ToastKind.Warning);
                }

                return;
            }

            _wantedChannel = new VoiceChannel(channelBase + GAME_CHANNEL_SUFFIX, ChatCapability.AudioOnly, positional: true);
            ReconcileChannels();
        }

        /// <summary>
        /// The Win/Lose scene: everyone is out of the building and the players' objects are gone, so
        /// the positional channel has nothing left to place a voice on (and its mix is kept silent —
        /// see MixVolume). Everyone moves to a plain group channel of the same match and hears
        /// everyone, like in the lobby.
        /// </summary>
        public void EnterEndScreenVoice()
        {
            string channelBase = lobbyManager != null && lobbyManager.JoinedLobby != null
                ? lobbyManager.JoinedLobby.Id
                : string.IsNullOrEmpty(_lastLanSessionKey) ? null : LAN_CHANNEL_PREFIX + _lastLanSessionKey;

            if (channelBase == null || !OnlineServices.IsAvailable) return;

            _wantedChannel = new VoiceChannel(channelBase + END_CHANNEL_SUFFIX, ChatCapability.AudioOnly, positional: false);
            ReconcileChannels();
        }

        /// <summary>Leaves the lobby/game channel. An open mic test stays open.</summary>
        public void LeaveVoiceChannel()
        {
            _wantedChannel = null;
            ReconcileChannels();
        }

        /// <summary>
        /// Adds the echo channel next to the lobby/game channel and sends the mic only there while the
        /// test is open: the player hears themselves and keeps hearing everyone, nobody hears the test,
        /// and nobody sees them leave. Leaving the game channel for the test used to strand every
        /// voice tap on rejoin.
        /// </summary>
        public void EnterTestVoiceChannel()
        {
            _wantsTestChannel = true;
            ReconcileChannels();
        }

        public void LeaveTestVoiceChannel()
        {
            _wantsTestChannel = false;
            ReconcileChannels();
        }

        private async void ReconcileChannels()
        {
            // The running walk re-reads the wanted state after every step, so it picks this up.
            if (_isReconciling) return;

            _isReconciling = true;
            float retryIn = -1f;

            try
            {
                await ReconcileChannelsAsync();

                if (_failedAttempts >= WarnAfterFailures)
                {
                    ScreenToast.Show("Chat de voz reconectado.", ToastKind.Info);
                }

                _failedAttempts = 0;
            }
            catch (Exception e)
            {
                // A failed login or join used to be the end of it: logged, and the player spent the
                // match without voice. It keeps trying, further apart each time, for as long as the
                // game still wants a channel.
                _failedAttempts++;
                retryIn = Mathf.Min(MaxRetrySeconds, FirstRetrySeconds * Mathf.Pow(2f, _failedAttempts - 1));

                if (_failedAttempts <= WarnAfterFailures || _failedAttempts % 10 == 0)
                {
                    Debug.LogWarning($"Voice channel: attempt {_failedAttempts} failed ({e.Message}). Trying again in {retryIn:0} s.");
                    LanDiagnosticsLog.Note($"VOZ: tentativa {_failedAttempts} falhou ({e.Message}); nova tentativa em {retryIn:0} s.");
                }

                if (_failedAttempts == WarnAfterFailures)
                {
                    ScreenToast.Show("Sem conexão com o chat de voz. Tentando reconectar...", ToastKind.Warning);
                }
            }
            finally
            {
                _isReconciling = false;
            }

            if (retryIn >= 0f && this != null && isActiveAndEnabled)
            {
                CancelInvoke(nameof(RetryReconcile));
                Invoke(nameof(RetryReconcile), retryIn);
            }
        }

        private void RetryReconcile()
        {
            if (IsReconciled) return;

            ReconcileChannels();
        }

        /// <summary>Vivox is where the game wants it: nothing left to join or leave.</summary>
        private bool IsReconciled =>
            _joinedChannel?.Name == _wantedChannel?.Name && _wantsTestChannel == _isInTestChannel;

        private async Task ReconcileChannelsAsync()
        {
            while (true)
            {
                if (_joinedChannel != null && _joinedChannel.Name != _wantedChannel?.Name)
                {
                    await LeaveJoinedChannelAsync();
                }
                else if (_joinedChannel == null && _wantedChannel != null)
                {
                    await JoinAsync(_wantedChannel);
                }
                else if (_wantsTestChannel && !_isInTestChannel)
                {
                    await JoinTestChannelAsync();
                }
                else if (!_wantsTestChannel && _isInTestChannel)
                {
                    await LeaveTestChannelAsync();
                }
                else return;
            }
        }

        private async Task JoinTestChannelAsync()
        {
            await EnsureLoggedInAsync();
            await VivoxService.Instance.JoinEchoChannelAsync(ECHO_CHANNEL_NAME, ChatCapability.AudioOnly);
            _isInTestChannel = true;

            // Vivox transmits to every joined channel by default; while testing, only the echo hears.
            await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, ECHO_CHANNEL_NAME);
            ApplyOutputVolume();
        }

        private async Task LeaveTestChannelAsync()
        {
            // The default back first, so the mic is never left talking to no channel at all.
            await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All);
            // Flagged so the ChannelLeft this raises is not taken for Vivox dropping the test.
            _leavingTestChannel = true;

            try
            {
                await LeaveIfActiveAsync(ECHO_CHANNEL_NAME);
            }
            finally
            {
                _leavingTestChannel = false;
            }

            _isInTestChannel = false;
            ApplyOutputVolume();
        }

        private async Task JoinAsync(VoiceChannel channel)
        {
            if (_reloginBeforeJoin)
            {
                if (VivoxService.Instance.IsLoggedIn)
                {
                    _isInTestChannel = false;
                    await VivoxService.Instance.LogoutAsync();
                }

                // Only once the logout went through: a failed one is tried again with the next join.
                _reloginBeforeJoin = false;
            }

            await EnsureLoggedInAsync();

            // A retry after a join that went through but failed to report it: Vivox refuses to join
            // the same channel twice, so take the one it is already in.
            if (VivoxService.Instance.ActiveChannels.ContainsKey(channel.Name))
            {
                _joinedChannel = channel;
                _currentChannelName = channel.Name;
                UnmuteOutput();
                ApplyOutputVolume();
                return;
            }

            if (channel.Positional)
            {
                Channel3DProperties properties = new Channel3DProperties(audibleDistance: Mathf.Max(channelAudibleDistance, audibleDistance), conversationalDistance: conversationalDistance,
                    audioFadeIntensityByDistanceaudio: audioFadeIntensity, audioFadeModel: audioFadeModel);

                await VivoxService.Instance.JoinPositionalChannelAsync(channel.Name, channel.Capability, properties);
            }
            else
            {
                await VivoxService.Instance.JoinGroupChannelAsync(channel.Name, channel.Capability);
            }

            _joinedChannel = channel;
            LanDiagnosticsLog.Note($"VOZ: entrou no canal {channel.Name}");
            _currentChannelName = channel.Name;
            UnmuteOutput();
            ApplyOutputVolume();
        }

        /// <summary>
        /// The mic setup mutes Vivox's output for as long as it is open and gives it back on the way
        /// out — but only as it found it, and a sheet closed before Vivox had logged in, or a run that
        /// died with it open, left it muted: every voice silent in the lobby and the match, with no
        /// way to tell. Nothing else in the game mutes it, so every channel joined opens it again.
        /// </summary>
        private static void UnmuteOutput()
        {
            try
            {
                if (VivoxService.Instance.IsOutputDeviceMuted) VivoxService.Instance.UnmuteOutputDevice();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not unmute the Vivox output: {e.Message}");
            }
        }

        private async Task LeaveJoinedChannelAsync()
        {
            string channelName = _joinedChannel.Name;

            // Cleared before the await: VivoxPlayer sends its 3D position every frame and must stop
            // the moment the channel starts going away, not after.
            _joinedChannel = null;
            _currentChannelName = null;

            await LeaveIfActiveAsync(channelName);
            ApplyOutputVolume();
        }

        /// <summary>
        /// In a match every voice is played by Unity through its tap, so Vivox's own output carries
        /// only the donation readings addressed to this player: there it plays at the readings'
        /// volume. Anywhere else — the lobby, the mic test's echo — it gets back the level the player
        /// set in the audio device panel.
        /// </summary>
        private void ApplyOutputVolume()
        {
            try
            {
                IVivoxService vivox = VivoxService.Instance;
                bool readings = _joinedChannel is { Positional: true } && !_isInTestChannel;

                if (readings)
                {
                    if (!_outputCarriesReadings) _playerOutputVolume = vivox.OutputDeviceVolume;

                    int volume = ConvertLinearToVivoxVolume(VoiceChatSettings.DonationTtsVolume);
                    if (vivox.OutputDeviceVolume != volume) vivox.SetOutputDeviceVolume(volume);
                }
                else if (_outputCarriesReadings)
                {
                    vivox.SetOutputDeviceVolume(_playerOutputVolume);
                }

                _outputCarriesReadings = readings;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not set the Vivox output volume: {e.Message}");
            }
        }

        private static async Task LeaveIfActiveAsync(string channelName)
        {
            if (!VivoxService.Instance.ActiveChannels.ContainsKey(channelName)) return;

            await VivoxService.Instance.LeaveChannelAsync(channelName);
        }
    
        /// <summary>Sets and remembers the volume this player hears <paramref name="playerId"/> at.</summary>
        public void SetParticipantVolume(string playerId, int volume)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            volume = Mathf.Clamp(volume, MIN_PARTICIPANT_VOLUME, MAX_PARTICIPANT_VOLUME);

            PlayerPrefs.SetInt(VOLUME_KEY_PREFIX + playerId, volume);

            foreach (KeyValuePair<string, ReadOnlyCollection<VivoxParticipant>> channel in VivoxService.Instance.ActiveChannels)
            {
                foreach (VivoxParticipant participant in channel.Value)
                {
                    if (participant.PlayerId == playerId)
                    {
                        participant.SetLocalVolume(MixVolume(participant));
                    }
                }
            }

            // The tap's AudioSource volume belongs to RemoteVoiceFilter, which also fades it with
            // distance every frame; it multiplies this in rather than having it overwritten.
            _tapVolumes[playerId] = ConvertVivoxVolumeToLinear(volume);
        }

        /// <summary>The volume saved for <paramref name="playerId"/>, on Vivox's -50..50 scale. 0 if never set.</summary>
        public int GetParticipantVolume(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return 0;

            return Mathf.Clamp(PlayerPrefs.GetInt(VOLUME_KEY_PREFIX + playerId, 0), MIN_PARTICIPANT_VOLUME, MAX_PARTICIPANT_VOLUME);
        }

        public bool IsParticipantLocallyMuted(string playerId)
        {
            return !string.IsNullOrEmpty(playerId) && PlayerPrefs.GetInt(MUTED_KEY_PREFIX + playerId, 0) == 1;
        }

        /// <summary>
        /// The volume this player chose for <paramref name="playerId"/>, as a linear gain — 0 while
        /// muted. The voice is heard only through its tap (silenced in the channel mix), so the tap
        /// is where mute has to land, whatever Vivox's own local mute does.
        /// </summary>
        public float GetParticipantTapVolume(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return 1f;
            if (IsParticipantLocallyMuted(playerId)) return 0f;

            if (!_tapVolumes.TryGetValue(playerId, out float volume))
            {
                volume = ConvertVivoxVolumeToLinear(GetParticipantVolume(playerId));
                _tapVolumes[playerId] = volume;
            }

            // After RemoteVoiceFilter has measured the voice: range and tiers stay the speaker's.
            return volume * Mathf.Pow(10f, GetSpeakerTrim(playerId) / 20f);
        }

        /// <summary>
        /// The dB (0 or less) <paramref name="playerId"/>'s own mic setup asked everyone to take off
        /// their voice, so a mic set hot enough to hurt does not reach anyone's ears that loud.
        /// </summary>
        public int GetSpeakerTrim(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return 0;

            return _speakerTrims.TryGetValue(playerId, out int trim) ? trim : 0;
        }

        /// <summary>The volume this player chose for <paramref name="playerId"/> plus that speaker's trim, on Vivox's scale.</summary>
        private int HeardVolume(string playerId)
        {
            return Mathf.Clamp(GetParticipantVolume(playerId) + GetSpeakerTrim(playerId), MIN_PARTICIPANT_VOLUME, MAX_PARTICIPANT_VOLUME);
        }

        /// <summary>
        /// What <paramref name="participant"/> plays at in Vivox's own mix. In the match's positional
        /// channel every voice is played by Unity through its tap (RemoteVoiceFilter), which carries
        /// the stream as it arrived whatever this says, so the mix is held at the floor. A voice with
        /// no tap yet — the first second of a match before the player's id replicates, the Win/Lose
        /// scene with every player object gone — used to come out of the mix at full level, with no
        /// distance, and then drop to the tap's level: the jump in volume at every transition.
        /// Anywhere else the mix is the voice: the volume this player chose plus the speaker's trim.
        /// </summary>
        private int MixVolume(VivoxParticipant participant)
        {
            if (IsPositionalChannel(participant.ChannelName)) return MIN_PARTICIPANT_VOLUME;

            return HeardVolume(participant.PlayerId);
        }

        // By name: participants arrive while the join is still being awaited, before _joinedChannel is set.
        private static bool IsPositionalChannel(string channelName)
        {
            return !string.IsNullOrEmpty(channelName) && channelName.EndsWith(GAME_CHANNEL_SUFFIX);
        }

        private void Lobby_OnLobbyUpdated()
        {
            var lobby = lobbyManager != null ? lobbyManager.JoinedLobby : null;
            if (lobby?.Players == null) return;

            foreach (Unity.Services.Lobbies.Models.Player player in lobby.Players)
            {
                int trim = 0;

                if (player.Data != null && player.Data.TryGetValue(Lobby.MIC_TRIM, out var data))
                    int.TryParse(data.Value, out trim);

                trim = MicSettings.ClampListenerTrim(trim);
                if (player.Id == null || (_speakerTrims.TryGetValue(player.Id, out int known) && known == trim)) continue;

                _speakerTrims[player.Id] = trim;

                // The tap reads it every frame; the channel mix has to be told.
                foreach (KeyValuePair<string, ReadOnlyCollection<VivoxParticipant>> channel in VivoxService.Instance.ActiveChannels)
                {
                    foreach (VivoxParticipant participant in channel.Value)
                    {
                        try
                        {
                            if (participant.IsSelf || participant.PlayerId != player.Id) continue;
                            participant.SetLocalVolume(MixVolume(participant));
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Could not apply the mic trim of {player.Id}: {e.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reapplies what this player chose for a participant every time Vivox hands over a fresh
        /// participant object — a rejoin, a channel switch, a new session.
        /// </summary>
        private void ApplySavedParticipantSettings(VivoxParticipant participant)
        {
            if (participant.IsSelf) return;

            try
            {
                int volume = MixVolume(participant);
                if (volume != 0) participant.SetLocalVolume(volume);

                if (IsParticipantLocallyMuted(participant.PlayerId)) participant.MutePlayerLocally();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not apply saved voice settings for {participant.DisplayName}: {e.Message}");
            }
        }

        /// <summary>
        /// This machine's own mic energy, from whichever channel it is in — the echo test included.
        /// The loudest of them: during the test the mic only reaches the echo channel, and the
        /// others may read it as silence. 0 while silent or not connected.
        /// </summary>
        public float GetSelfAudioEnergy()
        {
            if (!VivoxService.Instance.IsLoggedIn) return 0f;

            float energy = 0f;

            foreach (KeyValuePair<string, ReadOnlyCollection<VivoxParticipant>> channel in VivoxService.Instance.ActiveChannels)
            {
                foreach (VivoxParticipant participant in channel.Value)
                {
                    if (participant.IsSelf) energy = Mathf.Max(energy, (float)participant.AudioEnergy);
                }
            }

            return energy;
        }

        /// <summary>Written by RemoteVoiceFilter, on the AudioEnergy scale.</summary>
        public void SetHeardEnergy(string playerId, float energy)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            _heardEnergy[playerId] = energy;
        }

        /// <summary>
        /// How loud <paramref name="playerId"/> sounds here right now, 0..1 like AudioEnergy — the one
        /// to read for anyone else, whose own AudioEnergy Vivox leaves at 0 once they are tapped.
        /// </summary>
        public float GetHeardEnergy(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return 0f;

            return _heardEnergy.TryGetValue(playerId, out float energy) ? energy : 0f;
        }

        /// <summary>Whether <paramref name="playerId"/> is in the lobby/game channel this machine is in.</summary>
        public bool IsInCurrentChannel(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;

            return CurrentParticipants.Any(participant => participant.PlayerId == playerId);
        }

        private static float ConvertVivoxVolumeToLinear(int vivoxVolume)
        {
            vivoxVolume = Mathf.Clamp(vivoxVolume, MIN_PARTICIPANT_VOLUME, MAX_PARTICIPANT_VOLUME);

            return vivoxVolume >= 0 ? Mathf.Lerp(1f, 2f, vivoxVolume / 50f) : Mathf.Lerp(0f, 1f, (vivoxVolume + 50f) / 50f);
        }

        /// <summary>
        /// Vivox's device volumes are logarithmic with 0 as unchanged, so a linear 0..1 goes in as
        /// decibels: 0,5 is -6, and anything under -50 is the floor.
        /// </summary>
        private static int ConvertLinearToVivoxVolume(float linear)
        {
            if (linear <= 0.0032f) return MIN_PARTICIPANT_VOLUME;

            return Mathf.Clamp(Mathf.RoundToInt(20f * Mathf.Log10(linear)), MIN_PARTICIPANT_VOLUME, 0);
        }
        
        /// <summary>Mutes or unmutes <paramref name="playerId"/> for this player only, and remembers it.</summary>
        public void SetParticipantLocalMute(string playerId, bool isMuted)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            PlayerPrefs.SetInt(MUTED_KEY_PREFIX + playerId, isMuted ? 1 : 0);

            foreach (KeyValuePair<string, ReadOnlyCollection<VivoxParticipant>> currentChannel in VivoxService.Instance.ActiveChannels)
            {
                foreach (VivoxParticipant participant in currentChannel.Value)
                {
                    if (participant.PlayerId != playerId) continue;

                    if (isMuted) participant.MutePlayerLocally();
                    else participant.UnmutePlayerLocally();
                }
            }
        }
        
        public VivoxParticipant GetChannelParticipant(string playerId, string channelName = null)
        {
            channelName ??= _currentChannelName;
            if (string.IsNullOrEmpty(channelName)) return null;
            if (!VivoxService.Instance.ActiveChannels.TryGetValue(channelName, out var participants)) return null;

            return participants.FirstOrDefault(p => p.PlayerId == playerId);
        }

        private void VivoxService_OnParticipantAddedToChannel(VivoxParticipant participant)
        {
            if (participant.ChannelName == ECHO_CHANNEL_NAME) return;

            ApplySavedParticipantSettings(participant);
            OnParticipantJoinedChannel?.Invoke(participant);
        }

        private void VivoxService_OnParticipantRemovedFromChannel(VivoxParticipant participant)
        {
            if (participant.ChannelName == ECHO_CHANNEL_NAME) return;
            OnParticipantLeftChannel?.Invoke(participant);
        }

        private void VivoxService_OnChannelLeft(string channelName)
        {
            if (channelName == ECHO_CHANNEL_NAME)
            {
                // Dropped by Vivox rather than left: the walk below opens it again if still wanted.
                if (_isInTestChannel && !_leavingTestChannel) HandleDroppedChannel(channelName, isTest: true);
                return;
            }

            // A leave this machine asked for has already forgotten the channel before awaiting it,
            // so a channel still held here was dropped by Vivox - an RTP timeout, the network
            // blinking. It used to stay "joined": no voice for the rest of the match, never retried.
            if (_joinedChannel != null && _joinedChannel.Name == channelName)
            {
                HandleDroppedChannel(channelName, isTest: false);
            }

            OnLeftChannel?.Invoke(channelName);
        }

        private void HandleDroppedChannel(string channelName, bool isTest)
        {
            if (isTest)
            {
                _isInTestChannel = false;
            }
            else
            {
                _joinedChannel = null;
                _currentChannelName = null;
            }

            // Vivox keeps the dropped session in its group, and joining the same channel again is
            // refused ("Handle Taken") until the client logs out: the next join starts from a fresh login.
            _reloginBeforeJoin = true;

            Debug.LogWarning($"Voice channel {channelName} dropped by Vivox; joining it again.");
            LanDiagnosticsLog.Note($"VOZ: canal {channelName} caiu; entrando de novo.");

            ApplyOutputVolume();
            ReconcileChannels();
        }

        private async void Application_ApplicationQuit()
        {
            try
            {
                if (VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LogoutAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error logging out of Vivox on quit: {e.Message}");
            }
        }
        
        
        private void OnDisable()
        {
            VivoxService.Instance.ParticipantAddedToChannel -= VivoxService_OnParticipantAddedToChannel;
            VivoxService.Instance.ParticipantRemovedFromChannel -= VivoxService_OnParticipantRemovedFromChannel;
            VivoxService.Instance.ChannelLeft -= VivoxService_OnChannelLeft;
            VivoxService.Instance.EffectiveInputDeviceChanged -= MicSettings.ApplyToCurrentDevice;

            CancelInvoke(nameof(RetryReconcile));

            VoiceChatSettings.OnChanged -= ApplyOutputVolume;
            Application.quitting -= Application_ApplicationQuit;

            // Gone first when the game shuts down.
            if (Lobby.instance == null) return;

            Lobby.instance.OnJoinedLobby -= VivoxService_OnJoinedLobby;
            Lobby.instance.OnLeftLobby -= VivoxService_OnLeftLobby;
            Lobby.instance.OnLobbyUpdated -= Lobby_OnLobbyUpdated;
        }
        
    }
}