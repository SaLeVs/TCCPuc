using Missions;
using Player;
using Unity.Netcode;
using UnityEngine;

public class SoundMissionUi : MonoBehaviour
{
    [SerializeField] private GameObject missionPanel;
    [SerializeField] private SkillCheckController skillCheckController;

    private PlayerState _localPlayerState;
    private SoundMissionManager _currentMission;
    private bool _isOpen;

    private void Awake()
    {
        if (missionPanel != null)
            missionPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (skillCheckController != null)
        {
            skillCheckController.OnPuzzleComplete += SkillCheckController_OnPuzzleComplete;
        }
    }
    
    public void Open(SoundMissionManager mission)
    {
        if (_isOpen || mission == null) return;

        _localPlayerState = GetLocalPlayerState();
        
        if (_localPlayerState == null) return;

        _currentMission = mission;
        _isOpen = true;

        _localPlayerState.SetInputLocked(true);

        if (missionPanel != null)
        {
            missionPanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        skillCheckController.StartPuzzle();
    }

    public void Deactivate()
    {
        Close();
    }

    private void SkillCheckController_OnPuzzleComplete()
    {
        _currentMission?.NotifyPuzzleCompletedRpc();
        Close();
    }

    private void Close()
    {
        if (_localPlayerState != null)
        {
            _localPlayerState.SetInputLocked(false);
        }

        if (missionPanel != null)
        {
            missionPanel.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (skillCheckController != null)
        {
            skillCheckController.ResetPuzzle();
        }

        _localPlayerState = null;
        _currentMission = null;
        _isOpen = false;
    }

    private static PlayerState GetLocalPlayerState()
    {
        NetworkObject playerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        return playerObj != null && playerObj.TryGetComponent(out PlayerState ps) ? ps : null;
    }
    
    private void OnDisable()
    {
        if (skillCheckController != null)
        {
            skillCheckController.OnPuzzleComplete -= SkillCheckController_OnPuzzleComplete;
        }
    }
    
}