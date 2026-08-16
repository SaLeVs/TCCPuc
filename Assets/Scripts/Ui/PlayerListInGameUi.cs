using System.Collections.Generic;
using Inputs;
using Network;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace UI
{
    public class PlayerListInGameUi : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private GameObject playerListCanvas;
        [SerializeField] private RectTransform playerListContent;
        [SerializeField] private PlayerListItemUi playerListItemUi;
        [SerializeField] private bool includeSelfInList = true;

        private readonly Dictionary<string, PlayerListItemUi> _rosterEntries = new Dictionary<string, PlayerListItemUi>();

        private bool _isPlayerListOpen;

        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            inputReader.OnPlayerListEvent += InputReader_OnPlayerListPressed;

            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel += VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;
            }
        }

        
        private void InputReader_OnPlayerListPressed()
        {
            _isPlayerListOpen = !_isPlayerListOpen;

            Cursor.lockState = _isPlayerListOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isPlayerListOpen;

            playerListCanvas.SetActive(_isPlayerListOpen);
        }

        private void VivoxManager_OnParticipantJoined(VivoxParticipant participant)
        {
            if (!includeSelfInList && participant.IsSelf) return;
            if (_rosterEntries.ContainsKey(participant.PlayerId)) return;

            PlayerListItemUi entry = Instantiate(playerListItemUi, playerListContent);
            entry.Setup(participant, OnVolumeChanged, OnMuteChanged);
            _rosterEntries[participant.PlayerId] = entry;
        }

        private void VivoxManager_OnParticipantLeft(VivoxParticipant participant)
        {
            if (!_rosterEntries.TryGetValue(participant.PlayerId, out PlayerListItemUi entry)) return;

            Destroy(entry.gameObject);
            _rosterEntries.Remove(participant.PlayerId);
        }

        private void OnVolumeChanged(string playerId, int volume)
        {
            VivoxManager.instance?.SetParticipantVolume(playerId, volume);
        }
        
        private void OnMuteChanged(string playerId, bool isMuted)
        {
            VivoxManager.instance?.SetParticipantLocalMute(playerId, isMuted);
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            inputReader.OnPlayerListEvent -= InputReader_OnPlayerListPressed;

            if (VivoxManager.instance != null)
            {
                VivoxManager.instance.OnParticipantJoinedChannel -= VivoxManager_OnParticipantJoined;
                VivoxManager.instance.OnParticipantLeftChannel -= VivoxManager_OnParticipantLeft;
            }

            foreach (PlayerListItemUi entry in _rosterEntries.Values)
            {
                if (entry != null)
                {
                    Destroy(entry.gameObject);
                }
            }
            
            _rosterEntries.Clear();
        }
        
        
    }
}