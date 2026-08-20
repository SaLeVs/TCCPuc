using System;
using Player;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace UI
{
    public class PlayerInfosUi : MonoBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private TextMeshProUGUI playerNameText;

        private void Start()
        {
            PlayerInfos_OnPlayerNameChanged(string.Empty, playerState.PlayerInfos.PlayerName.Value);
            playerState.PlayerInfos.PlayerName.OnValueChanged += PlayerInfos_OnPlayerNameChanged;
        }

        private void PlayerInfos_OnPlayerNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
        {
            playerNameText.text = newName.ToString();
        }

        private void OnDestroy()
        {
            playerState.PlayerInfos.PlayerName.OnValueChanged -= PlayerInfos_OnPlayerNameChanged;
        }
        
    } 
}

