using System;
using UnityEngine;

namespace UI
{
    public class SkillCheckController : MonoBehaviour
    {
        [SerializeField] private CircularPointer pointer;
        [SerializeField] private SkillCheckGenerator generator;
        [SerializeField] private float successDistance = 15f;
        [SerializeField] private int requiredCorrectChecks = 3;

        public event Action OnPuzzleComplete;

        private int  _currentCorrectChecks;
        private bool _puzzleActive;

        
        public void StartPuzzle()
        {
            ResetPuzzle();
            _puzzleActive = true;
            generator.GenerateNewSlot();
        }

        public void ResetPuzzle()
        {
            _puzzleActive = false;
            _currentCorrectChecks = 0;
            generator.Reset();
        }
        
        
        public void Check()
        {
            if (!_puzzleActive || generator.CurrentSlot == null) return;

            Vector2 pointerPos = pointer.PointerRect.anchoredPosition;
            Vector2 slotPos = generator.CurrentSlot.Rect.anchoredPosition;
            bool success = Vector2.Distance(pointerPos, slotPos) <= successDistance;

            if (success)
            {
                Debug.Log($"SkillCheck: Correct, Slot: {generator.CurrentSlot.name}");
                _currentCorrectChecks++;

                if (_currentCorrectChecks >= requiredCorrectChecks)
                {
                    _puzzleActive = false;
                    Debug.Log("SkillCheck: Puzzle completed");
                    OnPuzzleComplete?.Invoke();
                    return;
                }

                generator.GenerateNewSlot();
            }
            else
            {
                Debug.Log("SkillCheck: Error");
            }
        }
        
    } 
}

