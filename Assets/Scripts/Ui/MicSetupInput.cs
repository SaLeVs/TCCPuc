using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Network;
using Unity.Services.Vivox;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// What the mic setup reads and drives: the microphones, how loud the voice is, the mic level and
    /// whether the player hears themselves. Vivox in the game; a simulated mic to run the whole sheet in
    /// the Editor without anyone talking.
    /// </summary>
    public interface IMicSetupInput
    {
        /// <summary>A microphone and a voice service to read it.</summary>
        bool IsAvailable { get; }

        /// <summary>The test is open: Energy is the player's voice.</summary>
        bool IsReady { get; }

        string CurrentDevice { get; }
        IReadOnlyList<string> Devices { get; }

        /// <summary>The player's own AudioEnergy, 0..1, five readings a second.</summary>
        float Energy { get; }

        /// <summary>The level saved for the microphone in use, in dB.</summary>
        int SavedVolume { get; }

        /// <summary>The list changed, or the microphone in use did.</summary>
        event Action DevicesChanged;

        void Begin();
        void End();
        void SetVolume(int volume);
        void Save(int volume);

        /// <summary>Puts the saved level back, for a sheet left without signing.</summary>
        void RestoreSaved();

        void SetHearSelf(bool hear);
        Task SelectDevice(string deviceName);
    }

    /// <summary>
    /// The real one: the mic test's echo channel next to whatever else is joined, the level through
    /// MicSettings. The echo comes back about half a second late, so the sheet only turns it on while
    /// the player chooses the mic and while they fine-tune.
    /// </summary>
    public class VivoxMicSetupInput : IMicSetupInput
    {
        // What Vivox lists besides the real microphones; they resolve to one of them.
        private static readonly string[] DefaultDeviceNames = { "Default System Device", "Default Communication Device" };

        private readonly VivoxManager _vivox;
        private bool _begun;
        private bool _outputWasMuted;

        public VivoxMicSetupInput(VivoxManager vivox)
        {
            _vivox = vivox;
        }

        public event Action DevicesChanged;

        public bool IsAvailable => _vivox != null
                                   && VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized
                                   && MicSettings.CurrentDeviceName != null;

        public bool IsReady => _vivox != null && _vivox.IsInMicTest;
        public string CurrentDevice => MicSettings.CurrentDeviceName;
        public IReadOnlyList<string> Devices => RealDevices().Select(device => device.DeviceName).ToList();
        public float Energy => _vivox != null ? _vivox.GetSelfAudioEnergy() : 0f;
        public int SavedVolume => MicSettings.CurrentInputVolume;

        public void Begin()
        {
            _begun = true;
            _outputWasMuted = VivoxService.Instance.IsOutputDeviceMuted;

            VivoxService.Instance.EffectiveInputDeviceChanged += VivoxService_OnDevicesChanged;
            VivoxService.Instance.AvailableInputDevicesChanged += VivoxService_OnDevicesChanged;

            SetHearSelf(false);
            _vivox.EnterTestVoiceChannel();
        }

        public void End()
        {
            if (!_begun) return;
            _begun = false;

            VivoxService.Instance.EffectiveInputDeviceChanged -= VivoxService_OnDevicesChanged;
            VivoxService.Instance.AvailableInputDevicesChanged -= VivoxService_OnDevicesChanged;

            // Even mid-join: the walk towards the echo channel turns around, and the mic is never left
            // talking to the echo alone.
            _vivox.LeaveTestVoiceChannel();

            SetMuted(_outputWasMuted);
        }

        public void SetVolume(int volume) => MicSettings.ApplyVolume(volume);

        public void Save(int volume)
        {
            MicSettings.SetCurrentInputVolume(volume);
            PlayerPrefs.Save();
        }

        public void RestoreSaved() => MicSettings.ApplyToCurrentDevice();

        /// <summary>
        /// Vivox's output carries the echo; the sheet comes before the menu's lobby, so nothing else is
        /// muted with it. A player who had it muted keeps it muted.
        /// </summary>
        public void SetHearSelf(bool hear)
        {
            if (_begun) SetMuted(!hear || _outputWasMuted);
        }

        public async Task SelectDevice(string deviceName)
        {
            VivoxInputDevice device = RealDevices().FirstOrDefault(candidate => candidate.DeviceName == deviceName);
            if (device == null) return;

            await VivoxService.Instance.SetActiveInputDeviceAsync(device);
        }

        private static void SetMuted(bool muted)
        {
            try
            {
                if (muted == VivoxService.Instance.IsOutputDeviceMuted) return;

                if (muted) VivoxService.Instance.MuteOutputDevice();
                else VivoxService.Instance.UnmuteOutputDevice();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicSetup] Could not {(muted ? "mute" : "unmute")} the Vivox output: {e.Message}");
            }
        }

        private static List<VivoxInputDevice> RealDevices()
        {
            return VivoxService.Instance.AvailableInputDevices
                .Where(device => !DefaultDeviceNames.Contains(device.DeviceName))
                .ToList();
        }

        // VivoxManager has already put the new microphone's own level on.
        private void VivoxService_OnDevicesChanged() => DevicesChanged?.Invoke();
    }

    /// <summary>
    /// A microphone for testing the sheet in the Editor: whoever drives the test says how loud the
    /// "player" is speaking, as the energy their voice would have at 0 dB, and this answers the way
    /// Vivox would — five readings a second, moved by the mic level at the VoiceProfile's dB per energy,
    /// over a quiet room. Set MicSetupScreen.InputOverride to one before the scene starts.
    /// </summary>
    public class SimulatedMicSetupInput : IMicSetupInput
    {
        public float DecibelsPerEnergy = 118f;
        public float NoiseFloor = 0.03f;

        /// <summary>The voice right now as its energy at 0 dB; below 0 is silence.</summary>
        public float VoiceAtZero = -1f;

        public bool HearSelf { get; private set; }
        public int Volume { get; private set; }
        public int Saved { get; private set; } = int.MinValue;

        private readonly List<string> _devices;
        private string _current;
        private float _readyAt = float.MaxValue;
        private float _nextReading;
        private float _energy;
        private int _syllable;

        public SimulatedMicSetupInput(params string[] devices)
        {
            _devices = devices.Length > 0 ? devices.ToList() : new List<string> { "Microfone simulado" };
            _current = _devices[0];
        }

        public event Action DevicesChanged;

        public bool IsAvailable => true;
        public bool IsReady => Time.unscaledTime >= _readyAt;
        public string CurrentDevice => _current;
        public IReadOnlyList<string> Devices => _devices;
        public int SavedVolume => Saved == int.MinValue ? 0 : Saved;

        public float Energy
        {
            get
            {
                if (Time.unscaledTime < _nextReading) return _energy;
                _nextReading = Time.unscaledTime + 0.2f;

                float floor = NoiseFloor + UnityEngine.Random.Range(-0.01f, 0.01f);

                // Speech is syllables and gaps: about one reading in five falls back to the room.
                bool gap = ++_syllable % 5 == 0;
                float voice = VoiceAtZero < 0f || gap
                    ? 0f
                    : VoiceAtZero + Volume / DecibelsPerEnergy + UnityEngine.Random.Range(-0.03f, 0.03f);

                _energy = Mathf.Clamp01(Mathf.Max(floor, voice));
                return _energy;
            }
        }

        public void Begin() => _readyAt = Time.unscaledTime + 0.6f;
        public void End() { }
        public void SetVolume(int volume) => Volume = MicSettings.Clamp(volume);
        public void Save(int volume) => Saved = volume;
        public void RestoreSaved() => Volume = SavedVolume;
        public void SetHearSelf(bool hear) => HearSelf = hear;

        public Task SelectDevice(string deviceName)
        {
            if (_devices.Contains(deviceName) && deviceName != _current)
            {
                _current = deviceName;
                Saved = int.MinValue;
                Volume = 0;
                DevicesChanged?.Invoke();
            }

            return Task.CompletedTask;
        }
    }
}
