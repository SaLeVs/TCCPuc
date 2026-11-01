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

        private string _playerId;
        private Action<string, int> _onVolumeChanged;
        private Action<string, bool> _onMuteChanged;


        /// <param name="isSelf">This machine's own player: listed, with nothing to adjust.</param>
        public void Setup(bool isSelf, Action<string, int> onVolumeChanged, Action<string, bool> onMuteChanged)
        {
            _onVolumeChanged = onVolumeChanged;
            _onMuteChanged = onMuteChanged;

            volumeSlider.gameObject.SetActive(!isSelf);
            muteToggle.gameObject.SetActive(!isSelf);

            if (isSelf) return;

            volumeSlider.minValue = -50;
            volumeSlider.maxValue = 50;

            // Asleep until the player's voice id arrives: it is what the choices are saved under.
            volumeSlider.interactable = false;
            muteToggle.interactable = false;

            muteToggle.onValueChanged.AddListener(HandleMuteChanged);
            volumeSlider.onValueChanged.AddListener(HandleSliderChanged);
        }

        /// <summary>
        /// Brings the row up to date. <paramref name="playerId"/> replicates some time after the player
        /// spawns; the saved volume and mute are loaded the first time it is there.
        /// </summary>
        /// <param name="hasVoice">Whether the player is in the voice channel right now. The row stays either way.</param>
        /// <param name="savedVolume">What this player last chose for them, on Vivox's -50..50 scale.</param>
        /// <param name="savedMuted">Whether this player last muted them.</param>
        public void Refresh(string displayName, string playerId, bool hasVoice, int savedVolume, bool savedMuted)
        {
            nameText.text = hasVoice ? displayName : $"{displayName} <color=#9a9a9a>(sem voz)</color>";

            if (string.IsNullOrEmpty(playerId) || playerId == _playerId) return;

            _playerId = playerId;

            muteToggle.SetIsOnWithoutNotify(savedMuted);
            muteToggle.interactable = true;

            volumeSlider.SetValueWithoutNotify(savedVolume);
            volumeSlider.interactable = !savedMuted;
        }


        private void HandleMuteChanged(bool isMuted)
        {
            volumeSlider.interactable = !isMuted;
            _onMuteChanged?.Invoke(_playerId, isMuted);
        }

        private void HandleSliderChanged(float value)
        {
            _onVolumeChanged?.Invoke(_playerId, Mathf.RoundToInt(value));
        }


        private void OnDestroy()
        {
            volumeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
            muteToggle.onValueChanged.RemoveListener(HandleMuteChanged);
        }


    }
}
