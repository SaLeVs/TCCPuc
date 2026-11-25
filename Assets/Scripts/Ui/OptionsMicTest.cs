using System;
using Components;
using Network;
using Unity.Services.Vivox;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// The options' TESTAR MICROFONE: the microphone read straight from the device and played back
    /// to the player a few tens of milliseconds behind them, at the mic level they set — the same way
    /// the mic setup sheet does it — with the level on the device panel's bar.
    ///
    /// <para>It used to join Vivox's echo channel. Outside a voice channel (the main menu) Vivox is
    /// not logged in and the echo is a login plus a join away, which failed or took long enough that
    /// nothing came back and the bar, which reads Vivox's own energy, never moved. Even when it worked
    /// the voice returned about a second late. While the test runs Vivox's microphone is muted, so
    /// whoever is in the lobby or the match with this player does not hear the test.</para>
    /// </summary>
    public class OptionsMicTest : MonoBehaviour
    {
        private const float WINDOW_SECONDS = 0.05f;

        /// <summary>The test running right now, or null.</summary>
        public static OptionsMicTest Active { get; private set; }

        private readonly MicrophoneLevel _microphone = new MicrophoneLevel();
        private MicMonitor _monitor;
        private string _device;
        private bool _mutedVivoxInput;

        /// <summary>The microphone right now in dBFS, before the mic level.</summary>
        public float LevelDb => _microphone.LevelDb;

        /// <summary>The mic level in dB, the gain Vivox puts on the voice for everyone else.</summary>
        public int Volume => MicSettings.CurrentInputVolume;

        /// <summary>Whether a microphone could be opened at all.</summary>
        public bool HasMicrophone => _microphone.Clip != null;

        public static OptionsMicTest Begin()
        {
            if (Active != null) return Active;

            var go = new GameObject(nameof(OptionsMicTest));
            return go.AddComponent<OptionsMicTest>();
        }

        public static void End()
        {
            OptionsMicTest active = Active;
            if (active == null) return;

            // At once, not at the end of the frame with the Destroy: a test started again in the same
            // frame must not find this one still there, nor Vivox's mic still muted by it.
            active.Shutdown();
            Destroy(active.gameObject);
        }

        private void Awake()
        {
            Active = this;

            _device = MicSettings.CurrentDeviceName;
            _microphone.Start(_device, WINDOW_SECONDS);

            if (!HasMicrophone)
            {
                ScreenToast.Show("Nenhum microfone encontrado.", ToastKind.Warning);
            }

            _monitor = MicMonitor.Create(() => _microphone.Clip, () => _microphone.Position);
            _monitor.transform.SetParent(transform, false);
            _monitor.SetLevel(Volume);
            _monitor.Listening = true;

            MuteVivoxInput();
        }

        private void Update()
        {
            if (Active != this) return;

            // The player may pick another microphone from the panel's list while testing.
            string device = MicSettings.CurrentDeviceName;
            if (device != _device)
            {
                _device = device;
                _microphone.Start(_device, WINDOW_SECONDS);
            }

            _microphone.Read();

            // Follows the panel's mic level slider as it moves.
            _monitor.SetLevel(Volume);
        }

        private void OnDestroy() => Shutdown();

        private void Shutdown()
        {
            if (Active == this) Active = null;

            if (_monitor != null) _monitor.Listening = false;
            _microphone.Stop();
            UnmuteVivoxInput();
        }

        private void MuteVivoxInput()
        {
            try
            {
                IVivoxService vivox = VivoxService.Instance;
                if (vivox == null || !vivox.IsLoggedIn || vivox.IsInputDeviceMuted) return;

                vivox.MuteInputDevice();
                _mutedVivoxInput = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OptionsMicTest] Could not mute the Vivox microphone: {e.Message}");
            }
        }

        private void UnmuteVivoxInput()
        {
            if (!_mutedVivoxInput) return;
            _mutedVivoxInput = false;

            try
            {
                VivoxService.Instance?.UnmuteInputDevice();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OptionsMicTest] Could not unmute the Vivox microphone: {e.Message}");
            }
        }
    }
}
