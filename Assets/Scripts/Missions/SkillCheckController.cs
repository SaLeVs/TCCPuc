using System;
using UI;
using UnityEngine;

public class SkillCheckController : MonoBehaviour
{
    [SerializeField] private RectTransform checkArea;
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
        if (!_puzzleActive || generator.CurrentSlot == null)
            return;

        Vector2 checkAreaPos = checkArea.position;
        Vector2 slotPos = generator.CurrentSlot.Rect.position;

        float distance = Vector2.Distance(checkAreaPos, slotPos);
        
        bool success = distance <= successDistance;

        Debug.Log($"Distance: {distance}");

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

