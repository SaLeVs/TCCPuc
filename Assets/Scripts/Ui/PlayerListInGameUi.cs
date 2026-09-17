using System.Collections.Generic;
using Network;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace UI
{
    public class PlayerListInGameUi : NetworkBehaviour
    {
        // The roster has no toggle of its own: it lives under the pause menu now, so PauseMenuUi
        // turning PauseCanvas on and off is what shows and hides it, cursor handling included.
        [SerializeField] private RectTransform playerListContent;
        [SerializeField] private PlayerListItemUi playerListItemUi;
        [SerializeField] private bool includeSelfInList = true;

        private readonly Dictionary<string, PlayerListItemUi> _rosterEntries = new Dictionary<string, PlayerListItemUi>();

        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            if (VivoxManager.instance == null) return;

            VivoxManager.instance.OnParticipantJoinedChannel += VivoxManager_OnParticipantJoined;
            VivoxManager.instance.OnParticipantLeftChannel += VivoxManager_OnParticipantLeft;

            RebuildParticipantList();
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
        
        private void RebuildParticipantList()
        {
            foreach (VivoxParticipant participant in VivoxManager.instance.CurrentParticipants)
            {
                VivoxManager_OnParticipantJoined(participant);
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

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