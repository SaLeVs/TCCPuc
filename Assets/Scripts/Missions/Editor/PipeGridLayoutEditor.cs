#if UNITY_EDITOR
using System.Collections.Generic;
using ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Missions.Editor
{
    [CustomEditor(typeof(PipeGridLayout))]
    public class PipeGridLayoutEditor : UnityEditor.Editor
    {
        private PipeGridLayout _layout;
        private int _selectedColumn = -1;
        private int _selectedRow = -1;

        private void OnEnable()
        {
            _layout = (PipeGridLayout)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("columns"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rows"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Grid do Puzzle", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Click on a cell to activate/select (green = active, cyan = selected).\nShift + click toggles directly.",
                MessageType.Info);

            DrawGrid();

            EditorGUILayout.Space(10);

            if (_selectedColumn >= 0 && _selectedRow >= 0)
            {
                DrawSelectedCellEditor();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Clean grid"))
            {
                if (EditorUtility.DisplayDialog("Clean grid", "Remove all cells from this layout?", "Yes", "Cancel"))
                {
                    Undo.RecordObject(_layout, "Clear Pipe Grid");
                    _layout.Editor_Clear();
                    _selectedColumn = -1;
                    _selectedRow = -1;
                    EditorUtility.SetDirty(_layout);
                }
            }
        }

        private void DrawGrid()
        {
            for (int row = 0; row < _layout.rows; row++)
            {
                EditorGUILayout.BeginHorizontal();

                for (int column = 0; column < _layout.columns; column++)
                {
                    bool hasCell = _layout.TryGetCell(column, row, out PipeCellData cell);
                    bool isSelected = _selectedColumn == column && _selectedRow == row;

                    Color previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = isSelected ? Color.cyan : (hasCell ? Color.green : Color.white);

                    if (GUILayout.Button($"{column},{row}", GUILayout.Width(32), GUILayout.Height(24)))
                    {
                        HandleCellClick(column, row, hasCell);
                    }

                    GUI.backgroundColor = previousColor;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void HandleCellClick(int column, int row, bool hasCell)
        {
            if (Event.current.shift)
            {
                Undo.RecordObject(_layout, "Toggle Pipe Cell");

                if (hasCell)
                {
                    _layout.Editor_RemoveCell(column, row);

                    if (_selectedColumn == column && _selectedRow == row)
                    {
                        _selectedColumn = -1;
                        _selectedRow = -1;
                    }
                }
                else
                {
                    _layout.Editor_SetCell(new PipeCellData
                    {
                        column = column,
                        row = row,
                        correctSteps = new List<int> { 0 }
                    });
                }

                EditorUtility.SetDirty(_layout);
                return;
            }

            if (!hasCell)
            {
                Undo.RecordObject(_layout, "Add Pipe Cell");
                _layout.Editor_SetCell(new PipeCellData
                {
                    column = column,
                    row = row,
                    correctSteps = new List<int> { 0 }
                });
                EditorUtility.SetDirty(_layout);
            }

            _selectedColumn = column;
            _selectedRow = row;
        }

        private void DrawSelectedCellEditor()
        {
            _layout.TryGetCell(_selectedColumn, _selectedRow, out PipeCellData cell);

            EditorGUILayout.LabelField($"Selected cell: column {_selectedColumn}, row {_selectedRow}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab (override)", cell.prefabOverride, typeof(GameObject), false);

            EditorGUILayout.LabelField("Correct Steps (índices de possibleAngles)");

            List<int> steps = cell.correctSteps ?? new List<int>();
            int removeIndex = -1;

            for (int i = 0; i < steps.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                steps[i] = EditorGUILayout.IntField(steps[i]);
                if (GUILayout.Button("-", GUILayout.Width(24))) removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0) steps.RemoveAt(removeIndex);

            if (GUILayout.Button("+ Add Correct Step"))
            {
                steps.Add(0);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_layout, "Edit Pipe Cell");
                cell.prefabOverride = newPrefab;
                cell.correctSteps = steps;
                _layout.Editor_SetCell(cell);
                EditorUtility.SetDirty(_layout);
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Remove Selected Cell"))
            {
                Undo.RecordObject(_layout, "Remove Pipe Cell");
                _layout.Editor_RemoveCell(_selectedColumn, _selectedRow);
                _selectedColumn = -1;
                _selectedRow = -1;
                EditorUtility.SetDirty(_layout);
            }
        }
    }
}
#endif