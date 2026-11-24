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

        private VivoxManager _vivox;
        private float _nextVivoxLookup;


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

            float energy = ResolveVivox() != null ? _vivox.GetSelfAudioEnergy() : 0f;

            if (energy >= _peak || Time.unscaledTime >= _peakUntil)
            {
                _peak = energy;
                _peakUntil = Time.unscaledTime + peakHoldSeconds;
            }

            label.text = $"{NameFor(_peak)}  {_peak:0.00}";
        }

        /// <summary>
        /// Found here rather than through VivoxManager.instance, which logs an error every time it
        /// finds nothing - every frame, in a scene opened without the boot (about a hundred a second).
        /// Looked up again at most once a second while missing.
        /// </summary>
        private VivoxManager ResolveVivox()
        {
            if (_vivox != null || Time.unscaledTime < _nextVivoxLookup) return _vivox;

            _nextVivoxLookup = Time.unscaledTime + 1f;
            _vivox = FindFirstObjectByType<VivoxManager>();

            return _vivox;
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
