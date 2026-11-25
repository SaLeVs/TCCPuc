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

        [Tooltip("The bar of the panel (AudioDeviceSettings.DeviceEnergyMask). While the options' mic test runs it shows the microphone read locally; otherwise AudioDeviceSettings fills it from Vivox. Found by name (MeterMask) when empty.")]
        [SerializeField] private Image meterFill;

        [Tooltip("How fast the bar follows the voice during the mic test.")]
        [SerializeField, Min(1f)] private float meterSpeed = 18f;

        [Tooltip("Seconds the loudest recent level stays on the label, so a shout can be read.")]
        [SerializeField, Min(0f)] private float peakHoldSeconds = 1.5f;

        private float _peak;
        private float _peakUntil;

        private VivoxManager _vivox;
        private float _nextVivoxLookup;
        private float _testEnergy;


        private void OnEnable()
        {
            _peak = 0f;
            _testEnergy = 0f;

            if (meterFill == null)
            {
                Transform meter = transform.Find("MeterMask");
                if (meter != null) meterFill = meter.GetComponent<Image>();
            }

            if (voiceProfile == null) return;

            PlaceMarker(speechMarker, voiceProfile.SpeechEnergy);
            PlaceMarker(shoutMarker, voiceProfile.ShoutEnergy);
        }

        private void Update()
        {
            if (label == null) return;

            float energy = TestEnergy(out bool testing);
            if (!testing) energy = ResolveVivox() != null ? _vivox.GetSelfAudioEnergy() : 0f;

            if (energy >= _peak || Time.unscaledTime >= _peakUntil)
            {
                _peak = energy;
                _peakUntil = Time.unscaledTime + peakHoldSeconds;
            }

            label.text = $"{NameFor(_peak)}  {_peak:0.00}";
        }

        /// <summary>
        /// The options' mic test, on the game's energy scale: the microphone level with the mic level on
        /// top, as the mic setup sheet reads it. 0 and false while no test runs.
        /// </summary>
        private float TestEnergy(out bool testing)
        {
            OptionsMicTest test = OptionsMicTest.Active;
            testing = test != null;
            if (!testing || voiceProfile == null) return 0f;

            return Mathf.Clamp01(voiceProfile.EnergyFromHeardLevel(test.LevelDb + test.Volume));
        }

        private void LateUpdate()
        {
            // After AudioDeviceSettings, which pulls the bar towards Vivox's reading every frame: the
            // mic is muted for Vivox during the test, so that would hold it at zero.
            float energy = TestEnergy(out bool testing);
            if (meterFill == null) return;

            if (!testing)
            {
                // Out of a voice channel nothing else moves the bar: it must not stay up after the test.
                if (_testEnergy > 0f) meterFill.fillAmount = 0f;
                _testEnergy = 0f;
                return;
            }

            _testEnergy = Mathf.Lerp(_testEnergy, energy, 1f - Mathf.Exp(-meterSpeed * Time.unscaledDeltaTime));
            meterFill.fillAmount = _testEnergy;
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
