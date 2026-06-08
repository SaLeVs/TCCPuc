using TMPro;
using UnityEngine;

namespace Missions
{
    public class MissionMessageUi : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;

        public void Setup(string message)
        {
            messageText.text = message;
        }
        
    }
}