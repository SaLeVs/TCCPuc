using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace UI
{
    public class SkillCheckGenerator : MonoBehaviour
    {
        [Header("Slot spawning")]
        [SerializeField] private SkillCheckSlot slotPrefab;
        [SerializeField] private RectTransform slotsParent;
        [SerializeField] private int correctAreasCount = 5;
        [SerializeField] private float radius = 100f;
        
        [SerializeField] private float minAngleGapDegrees = 10f;
        [SerializeField] private float rotationOffset;

        private readonly List<SkillCheckSlot> _slots = new();

        public SkillCheckSlot CurrentSlot { get; private set; }

        private void Awake() => Reset();

        private void OnDestroy() => ClearSlots();


        public void Reset()
        {
            CurrentSlot = null;
            GenerateSlots();
        }

        public void GenerateNewSlot()
        {
            if (CurrentSlot != null)
            {
                CurrentSlot.SetState(SkillCheckSlotState.Used);
            }

            List<SkillCheckSlot> available = _slots.FindAll(s => s.IsAvailable);

            if (available.Count == 0)
            {
                CurrentSlot = null;
                Debug.Log("SkillCheck: No slots available");
                return;
            }

            CurrentSlot = available[Random.Range(0, available.Count)];
            CurrentSlot.SetState(SkillCheckSlotState.Active);
        }

        private void GenerateSlots()
        {
            ClearSlots();

            if (slotPrefab == null || slotsParent == null || correctAreasCount <= 0) return;

            List<float> angles = GenerateRandomAngles(correctAreasCount, minAngleGapDegrees);

            foreach (float angle in angles)
            {
                SkillCheckSlot slot = Instantiate(slotPrefab, slotsParent);
                PositionSlot(slot.Rect, angle);
                slot.SetState(SkillCheckSlotState.Available);
                _slots.Add(slot);
            }
        }

        private void ClearSlots()
        {
            foreach (SkillCheckSlot slot in _slots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }

            _slots.Clear();
        }

        private void PositionSlot(RectTransform slotRect, float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;

            Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            slotRect.anchoredPosition = pos;

            Debug.Log($"Angle: {angleDegrees} Pos: {pos}");

            slotRect.localRotation = Quaternion.Euler(0f, 0f, angleDegrees + rotationOffset);
        }
        
        private List<float> GenerateRandomAngles(int count, float minGapDegrees)
        {
            List<float> result = new List<float>(count);

            float sectorSize = 360f / count;
            float baseOffset = Random.Range(0f, 360f);
            float jitterRange = Mathf.Max(0f, sectorSize - minGapDegrees);

            for (int i = 0; i < count; i++)
            {
                float sectorStart = i * sectorSize;
                float jitter = Random.Range(0f, jitterRange);
                float angle = (baseOffset + sectorStart + jitter) % 360f;

                result.Add(angle);
            }

            return result;
        }
    }
}