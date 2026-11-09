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
    /// What the mic setup reads and drives: the microphones, how loud the voice is right now, the mic
    /// level and whether the player hears themselves. Vivox in the game; a simulated mic to run the
    /// sheet in the Editor without anyone talking.
    /// </summary>
    public interface IMicSetupInput
    {
        /// <summary>A microphone and a voice service to save its level for.</summary>
        bool IsAvailable { get; }

        string CurrentDevice { get; }
        IReadOnlyList<string> Devices { get; }

        /// <summary>The level saved for the microphone in use, in dB.</summary>
        int SavedVolume { get; }

        /// <summary>
        /// The voice right now, in dBFS, before the mic level: read straight from the device every
        /// frame, without Vivox's processing or its five readings a second.
        /// </summary>
        float LevelDb { get; }

        /// <summary>The loudest sample of the last read, 0..1 of full scale: near 1 the device itself clips.</summary>
        float Peak { get; }

        /// <summary>Vivox's own AudioEnergy for this player — what the game reads — or -1 while there is none.</summary>
        float GameEnergy { get; }

        /// <summary>The list changed, or the microphone in use did.</summary>
        event Action DevicesChanged;

        void Begin();

        /// <summary>Reads the microphone; once a frame.</summary>
        void Tick();

        void End();
        void SetVolume(int volume);
        void Save(int volume, int listenerTrim);

        /// <summary>Puts the saved level back, for a sheet left without signing.</summary>
        void RestoreSaved();

        void SetHearSelf(bool hear);
        Task SelectDevice(string deviceName);
    }

    /// <summary>
    /// A microphone read with Unity's own capture, next to Vivox's: the level of the last few
    /// milliseconds as often as it is asked, with nothing in between. Vivox keeps the device too —
    /// Windows shares it.
    /// </summary>
    public class MicrophoneLevel
    {
        private const int SAMPLE_RATE = 48000;

        private string _device;
        private AudioClip _clip;
        private float[] _window = Array.Empty<float>();

        public float LevelDb { get; private set; } = -120f;
        public float Peak { get; private set; }

        public void Start(string deviceName, float windowSeconds)
        {
            Stop();
            if (Microphone.devices.Length == 0) return;

            _device = FindDevice(deviceName);
            Microphone.GetDeviceCaps(_device, out int min, out int max);
            int rate = min == 0 && max == 0 ? SAMPLE_RATE : Mathf.Clamp(SAMPLE_RATE, min, max);

            _clip = Microphone.Start(_device, true, 1, rate);
            _window = new float[Mathf.Max(64, Mathf.RoundToInt(rate * windowSeconds))];
        }

        public void Stop()
        {
            if (_clip != null) Microphone.End(_device);

            _clip = null;
            LevelDb = -120f;
            Peak = 0f;
        }

        public void Read()
        {
            if (_clip == null) return;

            int position = Microphone.GetPosition(_device);
            int length = _clip.samples;
            int count = _window.Length;
            if (position <= 0 || count > length) return;

            int start = position - count;

            if (start >= 0)
            {
                _clip.GetData(_window, start);
            }
            else
            {
                // The recording loops over one second: the window straddles its end.
                var tail = new float[-start];
                var head = new float[position];
                _clip.GetData(tail, length + start);
                _clip.GetData(head, 0);
                tail.CopyTo(_window, 0);
                head.CopyTo(_window, tail.Length);
            }

            double sum = 0;
            float peak = 0f;

            foreach (float sample in _window)
            {
                sum += sample * sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            LevelDb = 10f * Mathf.Log10(Mathf.Max((float)(sum / count), 1e-12f));
            Peak = peak;
        }

        /// <summary>
        /// Vivox's name for the device in Unity's list — the same Windows name, though some drivers
        /// cut it short — or the system default.
        /// </summary>
        private static string FindDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return null;

            string[] devices = Microphone.devices;

            return devices.FirstOrDefault(device => device == deviceName)
                   ?? devices.FirstOrDefault(device => deviceName.StartsWith(device) || device.StartsWith(deviceName))
                   ?? devices.FirstOrDefault(device => deviceName.Contains(device) || device.Contains(deviceName));
        }
    }

    /// <summary>
    /// The real one: the microphone read directly for the bar, and the mic test's echo channel joined
    /// the whole time so the player can hear themselves and Vivox reports what the game reads.
    /// </summary>
    public class VivoxMicSetupInput : IMicSetupInput
    {
        // What Vivox lists besides the real microphones; they resolve to one of them.
        private static readonly string[] DefaultDeviceNames = { "Default System Device", "Default Communication Device" };

        private readonly VivoxManager _vivox;
        private readonly MicrophoneLevel _microphone = new();
        private readonly float _windowSeconds;
        private bool _begun;
        private bool _outputWasMuted;

        public VivoxMicSetupInput(VivoxManager vivox, float windowSeconds)
        {
            _vivox = vivox;
            _windowSeconds = windowSeconds;
        }

        public event Action DevicesChanged;

        public bool IsAvailable => _vivox != null
                                   && VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized
                                   && MicSettings.CurrentDeviceName != null;

        public string CurrentDevice => MicSettings.CurrentDeviceName;
        public IReadOnlyList<string> Devices => RealDevices().Select(device => device.DeviceName).ToList();
        public int SavedVolume => MicSettings.CurrentInputVolume;
        public float LevelDb => _microphone.LevelDb;
        public float Peak => _microphone.Peak;
        public float GameEnergy => _vivox != null && _vivox.IsInMicTest ? _vivox.GetSelfAudioEnergy() : -1f;

        public void Begin()
        {
            _begun = true;
            _outputWasMuted = VivoxService.Instance.IsOutputDeviceMuted;

            VivoxService.Instance.EffectiveInputDeviceChanged += VivoxService_OnEffectiveInputDeviceChanged;
            VivoxService.Instance.AvailableInputDevicesChanged += VivoxService_OnAvailableInputDevicesChanged;

            _microphone.Start(CurrentDevice, _windowSeconds);
            _vivox.EnterTestVoiceChannel();
        }

        public void Tick() => _microphone.Read();

        public void End()
        {
            if (!_begun) return;
            _begun = false;

            VivoxService.Instance.EffectiveInputDeviceChanged -= VivoxService_OnEffectiveInputDeviceChanged;
            VivoxService.Instance.AvailableInputDevicesChanged -= VivoxService_OnAvailableInputDevicesChanged;

            _microphone.Stop();

            // Even mid-join: the walk towards the echo channel turns around, and the mic is never left
            // talking to the echo alone.
            _vivox.LeaveTestVoiceChannel();

            SetMuted(_outputWasMuted);
        }

        public void SetVolume(int volume) => MicSettings.ApplyVolume(volume);

        public void Save(int volume, int listenerTrim)
        {
            MicSettings.SetCurrentInputVolume(volume);
            MicSettings.SetCurrentListenerTrim(listenerTrim);
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
        private void VivoxService_OnEffectiveInputDeviceChanged()
        {
            if (_begun) _microphone.Start(CurrentDevice, _windowSeconds);
            DevicesChanged?.Invoke();
        }

        private void VivoxService_OnAvailableInputDevicesChanged() => DevicesChanged?.Invoke();
    }

    /// <summary>
    /// A microphone for testing the sheet in the Editor: whoever drives the test says how loud the
    /// "player" is speaking, in dBFS at the microphone, and this answers like a voice — syllables a few
    /// dB apart, a quiet room between words. Set MicSetupScreen.InputOverride to one before the scene
    /// starts.
    /// </summary>
    public class SimulatedMicSetupInput : IMicSetupInput
    {
        /// <summary>The voice right now in dBFS at the microphone; below -100 is silence.</summary>
        public float VoiceDb = -120f;

        public float RoomDb = -72f;

        public bool HearSelf { get; private set; }
        public int Volume { get; private set; }
        public int Saved { get; private set; } = int.MinValue;
        public int SavedTrim { get; private set; }

        private readonly List<string> _devices;
        private string _current;

        public SimulatedMicSetupInput(params string[] devices)
        {
            _devices = devices.Length > 0 ? devices.ToList() : new List<string> { "Microfone simulado" };
            _current = _devices[0];
        }

        public event Action DevicesChanged;

        public bool IsAvailable => true;
        public string CurrentDevice => _current;
        public IReadOnlyList<string> Devices => _devices;
        public int SavedVolume => Saved == int.MinValue ? 0 : Saved;
        public float LevelDb { get; private set; } = -120f;
        public float Peak { get; private set; }
        public float GameEnergy => -1f;

        public void Begin() { }

        public void Tick()
        {
            float t = Time.unscaledTime;
            float room = RoomDb + UnityEngine.Random.Range(-1.5f, 1.5f);

            if (VoiceDb < -100f)
            {
                LevelDb = room;
            }
            else
            {
                // About four syllables a second, each dipping up to 9 dB between them.
                float dip = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 3.7f * t), 2f);
                LevelDb = Mathf.Max(room, VoiceDb - 9f * dip + UnityEngine.Random.Range(-1.5f, 1.5f));
            }

            // Speech peaks sit about 10 dB over its RMS.
            Peak = Mathf.Min(1f, Mathf.Pow(10f, (LevelDb + 10f) / 20f));
        }

        public void End() { }
        public void SetVolume(int volume) => Volume = MicSettings.Clamp(volume);

        public void Save(int volume, int listenerTrim)
        {
            Saved = volume;
            SavedTrim = listenerTrim;
        }

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
