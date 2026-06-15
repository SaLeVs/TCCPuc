using Enums;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SkillCheckSlot : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private RectTransform rectTransform; 
        [SerializeField] private Color availableColor = Color.red;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color usedColor = Color.gray;

        public RectTransform Rect => rectTransform;
        public SkillCheckSlotState State { get; private set; }

        
        
        public void SetState(SkillCheckSlotState state)
        {
            State = state;

            switch (state)
            {
                case SkillCheckSlotState.Available:
                    image.color = availableColor;
                    break;

                case SkillCheckSlotState.Active:
                    image.color = activeColor;
                    break;

                case SkillCheckSlotState.Used:
                    image.color = usedColor;
                    break;
            }
        }

        public bool IsAvailable => State == SkillCheckSlotState.Available;
        
    }
}