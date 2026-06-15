using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SkillCheckSlot : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.red;

        private RectTransform rectTransform;

        public RectTransform Rect => rectTransform;
        public bool IsActive { get; private set; }

        private void Awake()
        {
            rectTransform = transform as RectTransform;
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            image.color = active ? activeColor : inactiveColor;
        }
        
    }
}