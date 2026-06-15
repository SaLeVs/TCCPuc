using Missions;
using Player;
using UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ui
{
    public class SoundMissionUi : MonoBehaviour
    {
        [SerializeField] private GameObject missionPanel;
        [SerializeField] private SkillCheckController skillCheckController;

        private PlayerState _localPlayerState;
        private SoundMissionManager  _currentManager;

        private void OnEnable()
        {
            SoundMissionManager.OnLocalPlayerInteracted += SoundMissionManager_OnLocalPlayerOpen;
            skillCheckController.OnPuzzleComplete += SkillCheckControler_OnPuzzleComplete;
        }

        private void SoundMissionManager_OnLocalPlayerOpen(SoundMissionManager manager)
        {
            _currentManager = manager;
            _localPlayerState = GetLocalPlayerState();

            _localPlayerState?.SetInputLocked(true);

            missionPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            skillCheckController.StartPuzzle();
        }
        
        public void Deactivate() => Close();

        private void SkillCheckControler_OnPuzzleComplete()
        {
            _currentManager?.NotifyPuzzleCompletedRpc();
            Close();
        }

        private void Close()
        {
            _localPlayerState?.SetInputLocked(false);

            missionPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            skillCheckController.ResetPuzzle();

            _localPlayerState = null;
            _currentManager = null;
        }

        private static PlayerState GetLocalPlayerState()
        {
            NetworkObject playerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            return playerObj != null && playerObj.TryGetComponent(out PlayerState ps) ? ps : null;
        }

        private void OnDisable()
        {
            SoundMissionManager.OnLocalPlayerInteracted -= SoundMissionManager_OnLocalPlayerOpen;
            skillCheckController.OnPuzzleComplete -= SkillCheckControler_OnPuzzleComplete;
        }
        
    }
}