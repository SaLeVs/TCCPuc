#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Missions.Puzzles;
using ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Missions.Editor
{
    // No PipesManager: o estado de encaixe de cada grid da lista e um atalho para encaixar todos.
    // O fluxo normal é pelo próprio grid (PipeGridLayoutEditor).
    [CustomEditor(typeof(PipesPuzzleManager))]
    public class PipesPuzzleManagerEditor : UnityEditor.Editor
    {
        private string _message;
        private MessageType _messageType;


        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PipesPuzzleManager manager = (PipesPuzzleManager)target;
            List<PipeGridLayout> layouts = PipeGridFitter.LayoutsOf(manager);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Encaixe dos grids", EditorStyles.boldLabel);

            if (layouts.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum grid em Possible Grid Layouts.", MessageType.None);
                return;
            }

            foreach (PipeGridLayout layout in layouts)
            {
                PipeGridFitStatus status = PipeGridFitter.Status(layout, manager);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(layout, typeof(PipeGridLayout), false);
                EditorGUILayout.LabelField(Label(status), GUILayout.Width(110));

                if (GUILayout.Button("Encaixar", GUILayout.Width(70)))
                {
                    FitAll(manager, new List<PipeGridLayout> { layout });
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Encaixar todos os grids"))
            {
                FitAll(manager, layouts);
            }

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, _messageType);
            }
        }

        private void FitAll(PipesPuzzleManager manager, List<PipeGridLayout> layouts)
        {
            StringBuilder message = new();
            _messageType = MessageType.Info;

            foreach (PipeGridLayout layout in layouts)
            {
                if (!PipeGridFitter.TryCalculate(layout, manager, out PipeFitResult result, out string error))
                {
                    message.AppendLine($"{layout.name}: {error}");
                    _messageType = MessageType.Warning;
                    continue;
                }

                Debug.Log(result.Report, layout);

                if (!result.CanApply)
                {
                    message.AppendLine(result.Report);
                    _messageType = MessageType.Warning;
                    continue;
                }

                PipeGridFitter.Apply(layout, manager, result);
                message.AppendLine($"{layout.name}: encaixado, maior folga {PipeSpawnFitSolver.Mm(result.MaxErrorAfter)}.");
            }

            _message = message.ToString().TrimEnd();
        }

        private static string Label(PipeGridFitStatus status) => status switch
        {
            PipeGridFitStatus.Fitted => "Encaixado",
            PipeGridFitStatus.Stale => "Desatualizado",
            _ => "Não encaixado"
        };
    }
}
#endif
