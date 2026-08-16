using System;
using TMPro;
using Unity.Services.Vivox;
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

        
        public void Setup(VivoxParticipant participant, Action<string, int> onVolumeChanged, Action<string, bool> onMuteChanged)
        {
            _playerId = participant.PlayerId;
            _onVolumeChanged = onVolumeChanged;
            _onMuteChanged = onMuteChanged;

            nameText.text = participant.DisplayName;

            muteToggle.SetIsOnWithoutNotify(false);
            muteToggle.onValueChanged.AddListener(HandleMuteChanged);

            volumeSlider.minValue = -50;
            volumeSlider.maxValue = 50;
            volumeSlider.SetValueWithoutNotify(participant.LocalVolume);
            volumeSlider.onValueChanged.AddListener(HandleSliderChanged);
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