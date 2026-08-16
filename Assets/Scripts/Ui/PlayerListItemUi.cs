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

        private string _playerId;
        private Action<string, int> _onVolumeChanged;

        public void Setup(VivoxParticipant participant, Action<string, int> onVolumeChanged)
        {
            _playerId = participant.PlayerId;
            _onVolumeChanged = onVolumeChanged;

            nameText.text = participant.DisplayName;

            volumeSlider.minValue = -50;
            volumeSlider.maxValue = 50;
            volumeSlider.SetValueWithoutNotify(participant.LocalVolume);
            volumeSlider.onValueChanged.AddListener(HandleSliderChanged);
        }

        private void HandleSliderChanged(float value)
        {
            _onVolumeChanged?.Invoke(_playerId, Mathf.RoundToInt(value));
        }

        private void OnDestroy()
        {
            volumeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
        }
    } 
}

