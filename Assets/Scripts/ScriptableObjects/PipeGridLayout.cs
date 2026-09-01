using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "PipeGridLayout", menuName = "ScriptableObjects/Pipes/Pipe Grid Layout")]
    public class PipeGridLayout : ScriptableObject
    {
        [Tooltip("Collums in grid")]
        [Min(1)] public int columns = 10;

        [Tooltip("Rows in grid")]
        [Min(1)] public int rows = 4;

        [SerializeField] private List<PipeCellData> cells = new();

        public IReadOnlyList<PipeCellData> Cells => cells;

        public bool TryGetCell(int column, int row, out PipeCellData cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].column == column && cells[i].row == row)
                {
                    cell = cells[i];
                    return true;
                }
            }

            cell = default;
            return false;
        }

#if UNITY_EDITOR
        public void Editor_SetCell(PipeCellData cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].column == cell.column && cells[i].row == cell.row)
                {
                    cells[i] = cell;
                    return;
                }
            }

            cells.Add(cell);
        }

        public void Editor_RemoveCell(int column, int row)
        {
            cells.RemoveAll(c => c.column == column && c.row == row);
        }

        public void Editor_Clear() => cells.Clear();
#endif
    }
    [Serializable]
    public struct PipeCellData
    {
        public int column;
        public int row;

        [Tooltip("Prefab to override the default pipe prefab for this cell.")]
        public GameObject prefabOverride;

        [Tooltip("Indices of possible angles considered 'correct' for this cell.")]
        public List<int> correctSteps;
    }
}