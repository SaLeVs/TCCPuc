using Components.Sound;
using Network;
using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// The mic test panel's level meter: the whole green-to-red bar always on screen and a white
    /// needle that follows the voice along it - left for a whisper, the middle for talking, the red
    /// end for a shout - and a label naming where the voice lands. Whether this player's whisper,
    /// normal talking and shout fall where they should is what decides how far their voice carries
    /// and when the monster hears it.
    ///
    /// <para>The needle reads the options' local mic test while it runs, and Vivox's own energy for
    /// this player otherwise (in the lobby or the match). The bar used to be a fill driven by Vivox
    /// alone, with fixed threshold marks: out of a voice channel nothing moved at all.</para>
    /// </summary>
    public class VoiceLevelMeter : MonoBehaviour
    {
        [SerializeField] private VoiceProfileSO voiceProfile;

        [Tooltip("The needle that follows the voice. Empty: the white speech marker becomes the needle.")]
        [SerializeField] private RectTransform levelNeedle;

        [Tooltip("Threshold marks of the old meter. The speech one is used as the needle when there is " +
                 "no needle of its own; the shout one is hidden then, so only one mark is on the bar.")]
        [SerializeField] private RectTransform speechMarker;
        [SerializeField] private RectTransform shoutMarker;

        [SerializeField] private Text label;

        [Tooltip("The bar of the panel (AudioDeviceSettings.DeviceEnergyMask). Kept whole: the needle " +
                 "shows the level now. Found by name (MeterMask) when empty.")]
        [SerializeField] private Image meterFill;

        [Tooltip("Width of the needle, in the meter's units.")]
        [SerializeField, Min(1f)] private float needleWidth = 5f;

        [Tooltip("How fast the needle rises with the voice, and how fast it falls back.")]
        [SerializeField, Min(1f)] private float riseSpeed = 30f;
        [SerializeField, Min(1f)] private float fallSpeed = 6f;

        [Tooltip("Seconds the loudest recent level stays on the label, so a shout can be read.")]
        [SerializeField, Min(0f)] private float peakHoldSeconds = 1.5f;

        private float _peak;
        private float _peakUntil;
        private float _shown;

        private VivoxManager _vivox;
        private float _nextVivoxLookup;


        private void OnEnable()
        {
            _peak = 0f;
            _shown = 0f;

            if (meterFill == null)
            {
                Transform meter = transform.Find("MeterMask");
                if (meter != null) meterFill = meter.GetComponent<Image>();
            }

            if (levelNeedle == null && speechMarker != null)
            {
                levelNeedle = speechMarker;
                if (shoutMarker != null) shoutMarker.gameObject.SetActive(false);
            }

            if (levelNeedle != null)
            {
                levelNeedle.gameObject.SetActive(true);
                levelNeedle.sizeDelta = new Vector2(needleWidth, levelNeedle.sizeDelta.y);
                levelNeedle.SetAsLastSibling();
            }

            PlaceNeedle(0f);
        }

        private void Update()
        {
            float energy = CurrentEnergy();

            if (energy >= _peak || Time.unscaledTime >= _peakUntil)
            {
                _peak = energy;
                _peakUntil = Time.unscaledTime + peakHoldSeconds;
            }

            if (label != null) label.text = $"{NameFor(_peak)}  {_peak:0.00}";
        }

        /// <summary>
        /// After AudioDeviceSettings, which pulls the bar's fill towards Vivox's reading every frame:
        /// the bar is put back whole and the needle moved instead.
        /// </summary>
        private void LateUpdate()
        {
            if (meterFill != null) meterFill.fillAmount = 1f;

            float energy = CurrentEnergy();
            float speed = energy > _shown ? riseSpeed : fallSpeed;
            _shown = Mathf.Lerp(_shown, energy, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));

            PlaceNeedle(_shown);
        }

        /// <summary>The voice now, 0..1 on the game's energy scale.</summary>
        private float CurrentEnergy()
        {
            OptionsMicTest test = OptionsMicTest.Active;

            // The options' test: the microphone level with the mic level on top, as the mic setup
            // sheet reads it.
            if (test != null)
            {
                return voiceProfile != null
                    ? Mathf.Clamp01(voiceProfile.EnergyFromHeardLevel(test.LevelDb + test.Volume))
                    : 0f;
            }

            return ResolveVivox() != null ? Mathf.Clamp01(_vivox.GetSelfAudioEnergy()) : 0f;
        }

        private void PlaceNeedle(float energy)
        {
            if (levelNeedle == null) return;

            levelNeedle.anchorMin = new Vector2(energy, 0f);
            levelNeedle.anchorMax = new Vector2(energy, 1f);

            // Kept whole inside the bar at both ends: silence sits on the left edge, not half past it.
            float halfWidth = levelNeedle.sizeDelta.x * 0.5f;
            levelNeedle.anchoredPosition = new Vector2(Mathf.Lerp(halfWidth, -halfWidth, energy), 0f);
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
    }
}
