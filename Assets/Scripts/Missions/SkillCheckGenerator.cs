using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace UI
{
    public class SkillCheckGenerator : MonoBehaviour
    {
        [SerializeField] private List<SkillCheckSlot> slots;

        public SkillCheckSlot CurrentSlot { get; private set; }

        private void Awake() => Reset();
        
        
        public void Reset()
        {
            CurrentSlot = null;
            foreach (SkillCheckSlot slot in slots)
            {
                slot.SetState(SkillCheckSlotState.Available);
            }
        }

        public void GenerateNewSlot()
        {
            if (CurrentSlot != null)
                CurrentSlot.SetState(SkillCheckSlotState.Used);

            List<SkillCheckSlot> available = slots.FindAll(s => s.IsAvailable);

            if (available.Count == 0)
            {
                CurrentSlot = null;
                Debug.Log("SkillCheck: No slots available");
                return;
            }

            CurrentSlot = available[Random.Range(0, available.Count)];
            CurrentSlot.SetState(SkillCheckSlotState.Active);
        }
    }

}
