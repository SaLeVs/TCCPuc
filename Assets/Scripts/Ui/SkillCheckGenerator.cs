using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace UI
{
    public class SkillCheckGenerator : MonoBehaviour
    {
        [SerializeField] private List<SkillCheckSlot> slots;

        public SkillCheckSlot CurrentSlot { get; private set; }

        private void Awake()
        {
            foreach (SkillCheckSlot slot in slots)
            {
                slot.SetState(SkillCheckSlotState.Available);
            }
        }

        public void GenerateNewSlot()
        {
            if (CurrentSlot != null)
            {
                CurrentSlot.SetState(SkillCheckSlotState.Used);
            }

            List<SkillCheckSlot> availableSlots = slots.FindAll(slot => slot.IsAvailable);

            if (availableSlots.Count == 0)
            {
                CurrentSlot = null;
                Debug.Log("No slots remaining.");
                return;
            }

            CurrentSlot = availableSlots[Random.Range(0, availableSlots.Count)];

            CurrentSlot.SetState(SkillCheckSlotState.Active);
        }
    }

}
