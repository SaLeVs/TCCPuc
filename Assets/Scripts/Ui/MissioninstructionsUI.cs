using ScriptableObjects;
using TMPro;
using UnityEngine;

public class MissioninstructionsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI instructionsText;

    public void Setup(MissionSO mission)
    {
        titleText.text = mission.missionName;
        instructionsText.text = mission.instructions;
    }
    
}
