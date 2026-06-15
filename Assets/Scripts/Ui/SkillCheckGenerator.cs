using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public class SkillCheckGenerator : MonoBehaviour
    {
        [SerializeField] private List<SkillCheckSlot> slots;

        public SkillCheckSlot CurrentSlot { get; private set; }

        public void GenerateNewSlot()
        {
            if (slots == null || slots.Count == 0)
            {
                Debug.LogWarning("No slots assigned in SkillCheckGenerator.");
                CurrentSlot = null;
                return;
            }

            foreach (SkillCheckSlot slot in slots)
            {
                if (slot != null)
                    slot.SetActive(false);
            }

            CurrentSlot = slots[Random.Range(0, slots.Count)];

            if (CurrentSlot != null)
                CurrentSlot.SetActive(true);
        }
    }

}
