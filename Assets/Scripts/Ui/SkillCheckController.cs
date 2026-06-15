using System;
using UnityEngine;

namespace UI
{
    public class SkillCheckController : MonoBehaviour
    {
        [SerializeField] private CircularPointer pointer;
        [SerializeField] private SkillCheckGenerator generator;
        [SerializeField] private float successDistance = 15f;

        private void Start()
        {
            generator.GenerateNewSlot();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Check();
            }
        }

        public void Check()
        {
            if (pointer == null || generator == null || generator.CurrentSlot == null)
            {
                Debug.LogWarning("Pointer, generator, or current slot is missing.");
                return;
            }

            Vector2 pointerPos = pointer.PointerRect.anchoredPosition;
            Vector2 slotPos = generator.CurrentSlot.Rect.anchoredPosition;

            float distance = Vector2.Distance(pointerPos, slotPos);
            bool success = distance <= successDistance;

            if (success)
            {
                Debug.Log($"Correct! in slot {generator.CurrentSlot.gameObject.name}");
                generator.GenerateNewSlot();
            }
            else
            {
                Debug.Log("Miss!");
            }
        }
    } 
}

