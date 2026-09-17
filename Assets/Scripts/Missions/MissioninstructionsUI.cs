using ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissioninstructionsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI instructionsText;
    [SerializeField] private Image missionImage;

    public void Setup(MissionSO mission)
    {
        titleText.text = mission.missionName;
        instructionsText.text = mission.instructions;
        missionImage.sprite = mission.missionImage;
    }
    
}
