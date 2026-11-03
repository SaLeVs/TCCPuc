using System;
using Unity.Services.Vivox;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// The microphone level this player set for each of their input devices, saved on this machine.
    ///
    /// <para>Vivox's automatic gain control stays off so that a whisper, normal talking and a shout
    /// keep their difference — and with it off, every mic went out as loud as its hardware and
    /// Windows made it: a boosted headset deafened everyone from the first word. The mic setup scene
    /// measures this level; the audio device panel's slider adjusts it; VivoxManager applies it to
    /// whichever device is in use. Vivox's input volume is one setting for every channel, so the
    /// menu's lobby, the 3D lobby and the match all get it.</para>
    /// </summary>
    public static class MicSettings
    {
        private const string INPUT_VOLUME_KEY_PREFIX = "MicInputVolume_";

        /// <summary>Vivox's range, in dB, 0 being the level the device delivers.</summary>
        public const int MIN_INPUT_VOLUME = -50;
        public const int MAX_INPUT_VOLUME = 50;

        /// <summary>
        /// Vivox advises against boosting the capture past this: the mic setup stops here, the
        /// slider can still go further by hand.
        /// </summary>
        public const int MAX_AUTOMATIC_BOOST = 25;

        /// <summary>
        /// The device Vivox actually captures from — with the system default selected, the one it
        /// resolves to — or null before Vivox is up or without a microphone.
        /// </summary>
        public static string CurrentDeviceName
        {
            get
            {
                try
                {
                    string deviceName = VivoxService.Instance?.EffectiveInputDevice?.DeviceName;
                    return string.IsNullOrEmpty(deviceName) ? null : deviceName;
                }
                catch (Exception)
                {
                    // Vivox has no effective device until it has listed them once.
                    return null;
                }
            }
        }

        /// <summary>
        /// The first time on a microphone: nobody measured or set its level on this machine yet.
        /// </summary>
        public static bool NeedsSetup
        {
            get
            {
                if (Application.isBatchMode) return false;

                string deviceName = CurrentDeviceName;
                return deviceName != null && !PlayerPrefs.HasKey(INPUT_VOLUME_KEY_PREFIX + deviceName);
            }
        }

        /// <summary>The level saved for the device in use, in dB. 0 (unchanged) if never set.</summary>
        public static int CurrentInputVolume => GetInputVolume(CurrentDeviceName);

        public static int GetInputVolume(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return 0;

            return Clamp(PlayerPrefs.GetInt(INPUT_VOLUME_KEY_PREFIX + deviceName, 0));
        }

        /// <summary>
        /// Keeps <paramref name="volume"/> for the device in use and applies it. Not written to disk
        /// here — a slider calls this every frame it is dragged; PlayerPrefs.Save is the caller's.
        /// </summary>
        public static void SetCurrentInputVolume(int volume)
        {
            string deviceName = CurrentDeviceName;
            if (deviceName == null) return;

            volume = Clamp(volume);
            PlayerPrefs.SetInt(INPUT_VOLUME_KEY_PREFIX + deviceName, volume);

            ApplyVolume(volume);
        }

        /// <summary>Puts the level saved for the device in use back on Vivox.</summary>
        public static void ApplyToCurrentDevice()
        {
            if (CurrentDeviceName == null) return;

            ApplyVolume(CurrentInputVolume);
        }

        /// <summary>
        /// Sets Vivox's input volume without saving it — the mic setup previews its adjustment this
        /// way, and ApplyToCurrentDevice undoes it if the player leaves without signing.
        /// </summary>
        public static void ApplyVolume(int volume)
        {
            try
            {
                volume = Clamp(volume);
                if (VivoxService.Instance.InputDeviceVolume != volume) VivoxService.Instance.SetInputDeviceVolume(volume);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not set the Vivox input volume: {e.Message}");
            }
        }

        public static int Clamp(int volume) => Mathf.Clamp(volume, MIN_INPUT_VOLUME, MAX_INPUT_VOLUME);
    }
}
