using UnityEngine;
using UnityEngine.UI;

namespace Systems
{
    public class ReadyIconUI : MonoBehaviour
    {
        [SerializeField] private Image icon;

        [SerializeField] private Color readyColor = new Color(0.2f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color notReadyColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);

        public void SetReady(bool isReady)
        {
            icon.color = isReady ? readyColor : notReadyColor;
        }
        
    }
}
