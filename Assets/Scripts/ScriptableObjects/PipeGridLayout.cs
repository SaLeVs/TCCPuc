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

        // Preenchido pelo botão "Encaixar spawns deste grid". Vazio = o grid mudou depois do último
        // encaixe (ou nunca foi encaixado), e as spawnPosition das células não valem.
        [SerializeField, HideInInspector] private string fittedSignature;

        public IReadOnlyList<PipeCellData> Cells => cells;

        /// <summary>Cada célula tem uma spawnPosition encaixada e o grid não mudou desde então.</summary>
        public bool HasFittedPositions => !string.IsNullOrEmpty(fittedSignature);

        public string FittedSignature => fittedSignature;

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
        // Mudar o grid invalida o encaixe: as posições salvas foram calculadas para o grid antigo.
        public void Editor_SetCell(PipeCellData cell)
        {
            fittedSignature = null;

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
            fittedSignature = null;
            cells.RemoveAll(c => c.column == column && c.row == row);
        }

        public void Editor_Clear()
        {
            fittedSignature = null;
            cells.Clear();
        }

        public void Editor_ApplyFit(IReadOnlyDictionary<Vector2Int, Vector3> positions, string signature)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                PipeCellData cell = cells[i];

                if (positions.TryGetValue(new Vector2Int(cell.column, cell.row), out Vector3 position))
                {
                    cell.spawnPosition = position;
                    cells[i] = cell;
                }
            }

            fittedSignature = signature;
        }
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

        [Tooltip("Onde o cano nasce, local ao SpawnList do PipesManager. Calculado pelo botão de encaixe.")]
        public Vector3 spawnPosition;
    }
}
