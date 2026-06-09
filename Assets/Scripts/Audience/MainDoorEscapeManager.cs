using Audience;
using Missions;
using Unity.Netcode;
using UnityEngine;

namespace Audience
{
    public class MainDoorEscapeManager : NetworkBehaviour
    {
        [SerializeField] private Animator escapeDoorAnimator;
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private AudienceManager audienceManager;
        
        [SerializeField] private string messageForAudiencePending;
        [SerializeField] private string messageForAllObjectivesCompletion;

        private static readonly int OpenHash = Animator.StringToHash("Open");

        private bool _mainMissionCompleted;
        private bool _audienceThresholdReached;
        private bool _doorOpened;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            missionManager.OnMainMissionCompleted += MissionManager_OnMainMissionCompleted;
            audienceManager.OnAudienceThresholdReached += MissionManager_OnAudienceThreshold;
        }

        private void MissionManager_OnMainMissionCompleted()
        {
            _mainMissionCompleted = true;

            UpdateObjectiveMessage();
            TryUnlockEscapeRoomDoor();
        }

        private void MissionManager_OnAudienceThreshold()
        {
            _audienceThresholdReached = true;

            UpdateObjectiveMessage();
            TryUnlockEscapeRoomDoor();
        }

        private void UpdateObjectiveMessage()
        {
            if (_mainMissionCompleted && _audienceThresholdReached)
            {
                missionManager.SendMessageToAllPlayers(messageForAllObjectivesCompletion);
                return;
            }

            if (!_audienceThresholdReached)
            {
                missionManager.SendMessageToAllPlayers(messageForAudiencePending);
            }
        }

        private void TryUnlockEscapeRoomDoor()
        {
            if (_doorOpened) return;
            if (!_mainMissionCompleted || !_audienceThresholdReached) return;

            _doorOpened = true;

            OpenEscapeRoomDoorRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void OpenEscapeRoomDoorRpc()
        {
            escapeDoorAnimator.SetTrigger(OpenHash);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            
            missionManager.OnMainMissionCompleted -= MissionManager_OnMainMissionCompleted;
            audienceManager.OnAudienceThresholdReached -= MissionManager_OnAudienceThreshold;
            
        }
        
    }
}
