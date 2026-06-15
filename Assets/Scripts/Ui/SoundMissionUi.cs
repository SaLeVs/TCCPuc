using Missions;
using UnityEngine;

namespace Ui
{
    public class SoundMissionUi : MonoBehaviour
    {
        [SerializeField] private GameObject missionPanel;

        
        private void OnEnable()  => SoundMissionManager.OnLocalPlayerInteracted += Open;
        

        private void Open()
        {
            missionPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            missionPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        
        private void OnDisable() => SoundMissionManager.OnLocalPlayerInteracted -= Open;
        
    }
}