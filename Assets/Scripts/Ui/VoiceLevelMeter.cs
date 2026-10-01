using Components.Sound;
using Network;
using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// Marks the voice profile's thresholds on the mic test panel's energy meter and names where the
    /// player's voice lands: whether this player's whisper, normal talking and shout fall on the right
    /// side of those marks is what decides how far their voice carries and when the monster hears it.
    /// The bar itself is the Vivox sample's AudioDeviceSettings meter.
    /// </summary>
    public class VoiceLevelMeter : MonoBehaviour
    {
        [SerializeField] private VoiceProfileSO voiceProfile;

        [Tooltip("Thin markers inside the meter, placed at the talking and shout thresholds.")]
        [SerializeField] private RectTransform speechMarker;
        [SerializeField] private RectTransform shoutMarker;

        [SerializeField] private Text label;

        [Tooltip("Seconds the loudest recent level stays on the label, so a shout can be read.")]
        [SerializeField, Min(0f)] private float peakHoldSeconds = 1.5f;

        private float _peak;
        private float _peakUntil;


        private void OnEnable()
        {
            _peak = 0f;

            if (voiceProfile == null) return;

            PlaceMarker(speechMarker, voiceProfile.SpeechEnergy);
            PlaceMarker(shoutMarker, voiceProfile.ShoutEnergy);
        }

        private void Update()
        {
            if (label == null) return;

            float energy = VivoxManager.instance != null ? VivoxManager.instance.GetSelfAudioEnergy() : 0f;

            if (energy >= _peak || Time.unscaledTime >= _peakUntil)
            {
                _peak = energy;
                _peakUntil = Time.unscaledTime + peakHoldSeconds;
            }

            label.text = $"{NameFor(_peak)}  {_peak:0.00}";
        }

        private string NameFor(float energy)
        {
            if (energy <= 0f) return "Silêncio";
            if (voiceProfile == null) return "Fala";
            if (energy >= voiceProfile.ShoutEnergy) return "Grito";
            if (energy >= voiceProfile.SpeechEnergy) return "Fala";

            return "Sussurro";
        }

        private static void PlaceMarker(RectTransform marker, float energy)
        {
            if (marker == null) return;

            marker.anchorMin = new Vector2(energy, 0f);
            marker.anchorMax = new Vector2(energy, 1f);
            marker.anchoredPosition = Vector2.zero;
        }
    }
}
