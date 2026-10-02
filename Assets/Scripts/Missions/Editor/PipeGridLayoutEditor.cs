#if UNITY_EDITOR
using System.Collections.Generic;
using Missions.Puzzles;
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

        private PipesPuzzleManager _manager;
        private bool _searchedManager;
        private string _message;
        private MessageType _messageType;

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
            DrawFitSection();
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
            List<float> angles = ManagerAngles();
            int removeIndex = -1;

            for (int i = 0; i < steps.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                steps[i] = EditorGUILayout.IntField(steps[i]);

                if (angles != null)
                {
                    string angle = steps[i] >= 0 && steps[i] < angles.Count ? $"= {angles[steps[i]]}°" : "fora da lista";
                    EditorGUILayout.LabelField(angle, GUILayout.Width(80));
                }

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

        // ---------- encaixe dos spawns ----------

        private void DrawFitSection()
        {
            EditorGUILayout.LabelField("Encaixe dos spawns", EditorStyles.boldLabel);

            if (!_searchedManager)
            {
                _manager = PipeGridFitter.FindManager(_layout);
                _searchedManager = true;
            }

            EditorGUI.BeginChangeCheck();
            _manager = (PipesPuzzleManager)EditorGUILayout.ObjectField(
                new GUIContent("Pipes Manager", "Prefab do PipesManager: de onde vêm os ângulos, o cano padrão e o SpawnList."),
                _manager, typeof(PipesPuzzleManager), false);

            if (EditorGUI.EndChangeCheck())
            {
                PipeGridFitter.Remember(_layout, _manager);
                _message = null;
            }

            PipeGridFitStatus status = PipeGridFitter.Status(_layout, _manager);
            EditorGUILayout.HelpBox(PipeGridFitter.StatusText(status),
                status == PipeGridFitStatus.Fitted ? MessageType.Info : MessageType.Warning);

            if (_manager == null)
            {
                EditorGUILayout.HelpBox("Escolha o prefab do PipesManager para encaixar.", MessageType.None);
                return;
            }

            if (!PipeGridFitter.IsInManager(_layout, _manager))
            {
                EditorGUILayout.HelpBox("Este grid ainda não está em Possible Grid Layouts do PipesManager: o jogo nunca vai sortear ele.",
                    MessageType.Warning);

                if (GUILayout.Button("Adicionar ao PipesManager"))
                {
                    PipeGridFitter.AddToManager(_layout, _manager);
                }
            }

            if (GUILayout.Button("Encaixar spawns deste grid", GUILayout.Height(28)))
            {
                Fit();
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Prévia na cena", "Mostra os canos na rotação correta embaixo do SpawnList. Nada é salvo.")))
            {
                _message = PipeGridFitter.ShowPreview(_layout, _manager, out string error) ? null : error;
                _messageType = MessageType.Warning;
            }

            using (new EditorGUI.DisabledScope(!PipeGridFitter.HasPreview(_manager)))
            {
                if (GUILayout.Button("Limpar prévia"))
                {
                    PipeGridFitter.ClearPreview(_manager);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, _messageType);
            }
        }

        private void Fit()
        {
            if (!PipeGridFitter.TryCalculate(_layout, _manager, out PipeFitResult result, out string error))
            {
                _message = error;
                _messageType = MessageType.Warning;
                return;
            }

            Debug.Log(result.Report, _layout);

            if (!result.CanApply)
            {
                _message = result.Report;
                _messageType = MessageType.Error;
                return;
            }

            PipeGridFitter.Apply(_layout, _manager, result);

            if (PipeGridFitter.HasPreview(_manager))
            {
                PipeGridFitter.ShowPreview(_layout, _manager, out _);
            }

            _message = result.Report + "\n\nPosições salvas neste grid.";
            _messageType = result.Notes.Count > 0 ? MessageType.Warning : MessageType.Info;
        }

        private List<float> ManagerAngles()
        {
            if (_manager == null) return null;

            SerializedProperty list = new SerializedObject(_manager).FindProperty("possibleAngles");
            List<float> angles = new();

            for (int i = 0; i < list.arraySize; i++) angles.Add(list.GetArrayElementAtIndex(i).floatValue);

            return angles;
        }
    }
}
#endif