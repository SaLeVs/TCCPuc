using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class LampsFeedback : NetworkBehaviour
    {
        [SerializeField] private LampsManager lampsManager;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Light statusLight;
        [SerializeField] private Color correctMaterial;
        [SerializeField] private Color incorrectMaterial;

        public override void OnNetworkSpawn()
        {
            lampsManager.OnCorrectLampsCountChanged += LampsManager_OnCorrectLampsCountChanged;
            lampsManager.OnMissionCompleteChanged += LampsManager_OnMissionCompleteChanged;
        }

        private void LampsManager_OnCorrectLampsCountChanged(int lampsCorrect, int totalLamps)
        {
            countText.text = $"{lampsCorrect}/{totalLamps}";
        }

        private void LampsManager_OnMissionCompleteChanged(bool isComplete)
        {
            statusLight.color = isComplete ? correctMaterial : incorrectMaterial;
        }

        public override void OnNetworkDespawn()
        {
            lampsManager.OnCorrectLampsCountChanged -= LampsManager_OnCorrectLampsCountChanged;
            lampsManager.OnMissionCompleteChanged -= LampsManager_OnMissionCompleteChanged;
        }
        
    }
}

