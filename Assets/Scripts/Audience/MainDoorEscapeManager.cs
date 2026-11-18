using System.Collections;
using Audience;
using Missions;
using Systems;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace Audience
{
    public class MainDoorEscapeManager : NetworkBehaviour
    {
        [Tooltip("Opcional. Sem porta, só manda as mensagens de objetivo.")]
        [SerializeField] private Animator escapeDoorAnimator;
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private AudienceManager audienceManager;
        
        [SerializeField] private string messageForAudiencePending;
        [SerializeField] private string messageForAllObjectivesCompletion;
        [SerializeField] private NavMeshSurface navMeshSurface;

        [Tooltip("Totem dos disquetes. Fica travado até as duas metas; vazio procura na cena.")]
        [SerializeField] private FloppyDiskTotem floppyDiskTotem;

        private static readonly int OpenHash = Animator.StringToHash("Open");

        private bool _mainMissionCompleted;
        private bool _audienceThresholdReached;
        private bool _doorOpened;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (floppyDiskTotem == null) floppyDiskTotem = FindAnyObjectByType<FloppyDiskTotem>();

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
            if (floppyDiskTotem != null)
                floppyDiskTotem.SetObjectivesProgress(_mainMissionCompleted, _audienceThresholdReached);

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
            // A porta é opcional: sem ela, este manager só manda as mensagens de objetivo (a saída
            // abre pelo EscapeRoomManager, com os disquetes no totem).
            if (escapeDoorAnimator == null) return;

            escapeDoorAnimator.SetTrigger(OpenHash);
            StartCoroutine(RebuildNavMeshAfterAnimation());
        }

        private IEnumerator RebuildNavMeshAfterAnimation()
        {
            yield return null;
            
            AnimatorStateInfo stateInfo = escapeDoorAnimator.GetCurrentAnimatorStateInfo(0);
            
            yield return new WaitForSeconds(stateInfo.length);

            navMeshSurface.BuildNavMesh();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            
            missionManager.OnMainMissionCompleted -= MissionManager_OnMainMissionCompleted;
            audienceManager.OnAudienceThresholdReached -= MissionManager_OnAudienceThreshold;
            
        }
        
    }
}
