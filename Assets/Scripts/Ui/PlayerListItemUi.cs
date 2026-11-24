using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerListItemUi : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle muteToggle;

        [Tooltip("The JAF symbol in front of the name.")]
        [SerializeField] private Image iconImage;

        [Header("Microphone")]
        [Tooltip("The image the mute button shows: the microphone, open or crossed out.")]
        [SerializeField] private Image micImage;

        [SerializeField] private Sprite micOnSprite;

        [Tooltip("Shown when this player is muted, or turned all the way down.")]
        [SerializeField] private Sprite micOffSprite;

        [SerializeField] private Color micOnColor = new Color(0.95f, 0.70f, 0.24f, 1f);
        [SerializeField] private Color micOffColor = new Color(0.78f, 0.27f, 0.24f, 1f);
        [SerializeField] private Color silentFillColor = new Color(0.45f, 0.42f, 0.40f, 1f);

        private string _playerId;
        private Action<string, int> _onVolumeChanged;
        private Action<string, bool> _onMuteChanged;

        /// <summary>The player's JAF symbol. Null leaves the row's own image alone.</summary>
        public void SetIcon(Sprite icon)
        {
            if (iconImage == null || icon == null || iconImage.sprite == icon) return;

            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }


        /// <param name="isSelf">This machine's own player: listed, with nothing to adjust.</param>
        public void Setup(bool isSelf, Action<string, int> onVolumeChanged, Action<string, bool> onMuteChanged)
        {
            _onVolumeChanged = onVolumeChanged;
            _onMuteChanged = onMuteChanged;

            volumeSlider.gameObject.SetActive(!isSelf);
            muteToggle.gameObject.SetActive(!isSelf);

            if (isSelf)
            {
                // No volume line under it: the name sits in the middle of the row instead of on top.
                RectTransform nameRect = nameText.rectTransform;
                RectTransform row = (RectTransform)transform;
                nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, -row.rect.height * 0.5f);
                return;
            }

            volumeSlider.minValue = -50;
            volumeSlider.maxValue = 50;

            // Asleep until the player's voice id arrives: it is what the choices are saved under.
            volumeSlider.interactable = false;
            muteToggle.interactable = false;

            muteToggle.onValueChanged.AddListener(HandleMuteChanged);
            volumeSlider.onValueChanged.AddListener(HandleSliderChanged);

            UpdateMicIcon();
        }

        /// <summary>
        /// Brings the row up to date. <paramref name="playerId"/> replicates some time after the player
        /// spawns; the saved volume and mute are loaded the first time it is there.
        /// </summary>
        /// <param name="hasVoice">Whether the player is in the voice channel right now. The row stays either way.</param>
        /// <param name="savedVolume">What this player last chose for them, on Vivox's -50..50 scale.</param>
        /// <param name="savedMuted">Whether this player last muted them.</param>
        /// <param name="status">Shown after the name, already formatted: whether they are dead and whether
        /// this player hears them. Null or empty for nothing.</param>
        public void Refresh(string displayName, string playerId, bool hasVoice, int savedVolume, bool savedMuted,
            string status = null)
        {
            string text = hasVoice ? displayName : $"{displayName} <color=#9a9a9a>(sem voz)</color>";
            if (!string.IsNullOrEmpty(status)) text += $" {status}";

            nameText.text = text;

            if (string.IsNullOrEmpty(playerId) || playerId == _playerId) return;

            _playerId = playerId;

            muteToggle.SetIsOnWithoutNotify(savedMuted);
            muteToggle.interactable = true;

            volumeSlider.SetValueWithoutNotify(savedVolume);
            volumeSlider.interactable = !savedMuted;

            UpdateMicIcon();
        }


        private void HandleMuteChanged(bool isMuted)
        {
            volumeSlider.interactable = !isMuted;
            _onMuteChanged?.Invoke(_playerId, isMuted);
            UpdateMicIcon();
        }

        private void HandleSliderChanged(float value)
        {
            _onVolumeChanged?.Invoke(_playerId, Mathf.RoundToInt(value));
            UpdateMicIcon();
        }

        /// <summary>
        /// The microphone on the mute button: open while this player hears them, crossed out once
        /// they are muted or the volume is all the way down - either way nothing is heard.
        /// </summary>
        private void UpdateMicIcon()
        {
            if (micImage == null) return;

            bool silent = muteToggle.isOn || volumeSlider.value <= volumeSlider.minValue;
            Sprite sprite = silent ? micOffSprite : micOnSprite;

            if (sprite != null) micImage.sprite = sprite;
            micImage.color = silent ? micOffColor : micOnColor;
            micImage.preserveAspect = true;

            // The bar goes grey with it, so a muted player does not read as turned up.
            if (volumeSlider.fillRect != null && volumeSlider.fillRect.TryGetComponent(out Image fill))
            {
                fill.color = silent ? silentFillColor : micOnColor;
            }
        }


        private void OnDestroy()
        {
            volumeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
            muteToggle.onValueChanged.RemoveListener(HandleMuteChanged);
        }
    }
}
