using System.Collections;
using Audience;
using Missions;
using Unity.AI.Navigation;
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
        [SerializeField] private NavMeshSurface navMeshSurface;

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
            StartCoroutine(RebuildNavMeshAfterAnimation());
        }

        private IEnumerator RebuildNavMeshAfterAnimation()
        {
            yield return null;
            
            AnimatorStateInfo stateInfo = escapeDoorAnimator.GetCurrentAnimatorStateInfo(0);
            
            yield return new WaitForSeconds(stateInfo.length);

            navMeshSurface.BuildNavMesh();
            Debug.Log("Build NavMesh after door opened");
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            
            missionManager.OnMainMissionCompleted -= MissionManager_OnMainMissionCompleted;
            audienceManager.OnAudienceThresholdReached -= MissionManager_OnAudienceThreshold;
            
        }
        
    }
}
