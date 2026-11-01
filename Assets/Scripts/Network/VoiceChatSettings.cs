using System;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// What this player chose to hear of the donation readings, saved on this machine.
    ///
    /// <para>A reading reaches a player two ways: the one addressed to them through Vivox's own output
    /// (VivoxManager scales it), everyone else's through that player's voice tap (RemoteVoiceFilter
    /// scales it while the reading lasts). Both read the choice here, so one slider covers both.</para>
    /// </summary>
    public static class VoiceChatSettings
    {
        private const string DONATION_TTS_ENABLED_KEY = "DonationTtsEnabled";
        private const string DONATION_TTS_VOLUME_KEY = "DonationTtsVolume";

        public static event Action OnChanged;

        public static bool DonationTtsEnabled { get; private set; } = PlayerPrefs.GetInt(DONATION_TTS_ENABLED_KEY, 1) == 1;

        /// <summary>Linear, 0..1.</summary>
        public static float DonationTtsVolume { get; private set; } = PlayerPrefs.GetFloat(DONATION_TTS_VOLUME_KEY, 0.5f);

        /// <summary>The gain a reading plays at here: the chosen volume, or 0 while readings are off.</summary>
        public static float DonationTtsGain => DonationTtsEnabled ? DonationTtsVolume : 0f;

        public static void SetDonationTtsEnabled(bool enabled)
        {
            DonationTtsEnabled = enabled;
            PlayerPrefs.SetInt(DONATION_TTS_ENABLED_KEY, enabled ? 1 : 0);
            OnChanged?.Invoke();
        }

        public static void SetDonationTtsVolume(float volume)
        {
            DonationTtsVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(DONATION_TTS_VOLUME_KEY, DonationTtsVolume);
            OnChanged?.Invoke();
        }
    }
}
