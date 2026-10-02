#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Missions.Puzzles;
using ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Missions.Editor
{
    public enum PipeGridFitStatus
    {
        NotFitted,
        Stale,
        Fitted
    }

    // Liga um grid ao PipesManager: monta o cálculo de encaixe (PipeSpawnFitSolver), salva as posições
    // encaixadas no próprio grid e mostra uma prévia dos canos na cena. Usado pelos dois inspectors.
    public static class PipeGridFitter
    {
        private const string PreviewName = "[Prévia do grid]";
        private const string ManagerPrefKey = "PipeGridFitter.Manager.";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;


        public static bool TryCalculate(PipeGridLayout layout, PipesPuzzleManager manager, out PipeFitResult result, out string error)
        {
            result = null;

            if (!TryReadManager(manager, out ManagerData data, out error)) return false;

            Dictionary<GameObject, PipeFitPiece> pieces = new();
            List<PipeFitCell> cells = new();

            foreach (PipeCellData cell in layout.Cells)
            {
                cells.Add(new PipeFitCell
                {
                    Column = cell.column,
                    Row = cell.row,
                    Piece = Piece(PrefabOf(cell, data), pieces),
                    CorrectSteps = cell.correctSteps != null ? new List<int>(cell.correctSteps) : new List<int>(),
                    Reference = data.Reference(cell.column, cell.row)
                });
            }

            if (cells.Count == 0)
            {
                error = "O grid está vazio.";
                return false;
            }

            float captureDistance = Mathf.Max(data.ColumnStep.magnitude, data.RowStep.magnitude) * 0.6f;

            result = PipeSpawnFitSolver.Solve(layout.name, layout.columns, layout.rows, cells, data.Angles,
                data.ColumnStep, data.RowStep, FixedPieces(data), captureDistance);
            return true;
        }

        public static void Apply(PipeGridLayout layout, PipesPuzzleManager manager, PipeFitResult result)
        {
            Dictionary<Vector2Int, Vector3> positions = result.Positions.ToDictionary(
                pair => new Vector2Int(pair.Key.Column, pair.Key.Row), pair => pair.Value);

            Undo.RecordObject(layout, "Encaixar spawns do grid");
            layout.Editor_ApplyFit(positions, Signature(layout, manager));
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssetIfDirty(layout);
        }

        // Fitted = as posições salvas valem para o grid e o PipesManager de agora.
        public static PipeGridFitStatus Status(PipeGridLayout layout, PipesPuzzleManager manager)
        {
            if (!layout.HasFittedPositions) return PipeGridFitStatus.NotFitted;
            if (manager == null) return PipeGridFitStatus.Fitted;

            return layout.FittedSignature == Signature(layout, manager) ? PipeGridFitStatus.Fitted : PipeGridFitStatus.Stale;
        }

        public static string StatusText(PipeGridFitStatus status) => status switch
        {
            PipeGridFitStatus.Fitted => "Encaixado: no jogo os canos nascem nas posições salvas neste grid.",
            PipeGridFitStatus.Stale => "Desatualizado: os ângulos, o cano padrão ou o SpawnList do PipesManager mudaram depois do encaixe. Encaixe de novo.",
            _ => "Não encaixado (ou o grid mudou depois do encaixe). No jogo ele usa a grade do SpawnList e os canos não encaixam."
        };

        // O PipesManager deste grid: o escolhido antes, senão o que já lista o grid, senão o único do projeto.
        public static PipesPuzzleManager FindManager(PipeGridLayout layout)
        {
            string layoutGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(layout));
            string remembered = EditorPrefs.GetString(ManagerPrefKey + layoutGuid, null);

            if (!string.IsNullOrEmpty(remembered))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(remembered));
                PipesPuzzleManager rememberedManager = prefab != null ? prefab.GetComponentInChildren<PipesPuzzleManager>(true) : null;

                if (rememberedManager != null) return rememberedManager;
            }

            List<PipesPuzzleManager> managers = AllManagerPrefabs();

            return managers.FirstOrDefault(m => IsInManager(layout, m)) ?? (managers.Count == 1 ? managers[0] : null);
        }

        public static void Remember(PipeGridLayout layout, PipesPuzzleManager manager)
        {
            string layoutGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(layout));
            string managerPath = manager != null ? AssetDatabase.GetAssetPath(manager) : null;

            if (string.IsNullOrEmpty(managerPath))
            {
                EditorPrefs.DeleteKey(ManagerPrefKey + layoutGuid);
                return;
            }

            EditorPrefs.SetString(ManagerPrefKey + layoutGuid, AssetDatabase.AssetPathToGUID(managerPath));
        }

        public static bool IsInManager(PipeGridLayout layout, PipesPuzzleManager manager)
        {
            SerializedProperty list = new SerializedObject(manager).FindProperty("possibleGridLayouts");

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == layout) return true;
            }

            return false;
        }

        public static void AddToManager(PipeGridLayout layout, PipesPuzzleManager manager)
        {
            // Prefab na Project: mexer direto no asset e salvar some no reimport, então edita uma cópia carregada.
            if (EditorUtility.IsPersistent(manager))
            {
                string path = AssetDatabase.GetAssetPath(manager);
                GameObject root = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    AppendLayout(root.GetComponentInChildren<PipesPuzzleManager>(true), layout);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return;
            }

            AppendLayout(manager, layout);
        }

        private static void AppendLayout(PipesPuzzleManager manager, PipeGridLayout layout)
        {
            SerializedObject managerObject = new(manager);
            SerializedProperty list = managerObject.FindProperty("possibleGridLayouts");

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = layout;
            managerObject.ApplyModifiedProperties();
        }

        public static List<PipeGridLayout> LayoutsOf(PipesPuzzleManager manager)
        {
            SerializedProperty list = new SerializedObject(manager).FindProperty("possibleGridLayouts");
            List<PipeGridLayout> layouts = new();

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is PipeGridLayout layout) layouts.Add(layout);
            }

            return layouts;
        }

        // ---------- prévia ----------

        // Mostra os canos do grid, já na rotação correta, embaixo do SpawnList do PipesManager aberto
        // na cena ou no Prefab Mode. São só meshes, marcados para nunca serem salvos.
        public static bool ShowPreview(PipeGridLayout layout, PipesPuzzleManager manager, out string error)
        {
            PipesPuzzleManager target = SceneInstance(manager);

            if (target == null)
            {
                error = "Abra o PipesManager no Prefab Mode (ou uma cena com ele) para ver a prévia.";
                return false;
            }

            if (!TryReadManager(target, out ManagerData data, out error)) return false;

            ClearPreview(manager);

            GameObject preview = new(PreviewName) { hideFlags = HideFlags.DontSave };
            preview.transform.SetParent(data.SpawnList, false);

            Dictionary<GameObject, PipeFitPiece> pieces = new();

            foreach (PipeCellData cell in layout.Cells)
            {
                GameObject prefab = PrefabOf(cell, data);
                PipeFitPiece piece = Piece(prefab, pieces);

                if (piece == null || cell.correctSteps == null || cell.correctSteps.Count == 0) continue;

                int step = Mathf.Clamp(cell.correctSteps[0], 0, data.Angles.Count - 1);

                GameObject pipe = new($"{cell.column},{cell.row} {prefab.name}") { hideFlags = HideFlags.DontSave };
                pipe.transform.SetParent(preview.transform, false);
                pipe.transform.localPosition = layout.HasFittedPositions ? cell.spawnPosition : data.Reference(cell.column, cell.row);
                pipe.transform.localRotation = Quaternion.AngleAxis(data.Angles[step], piece.RotationAxis);

                CopyModel(prefab, pipe.transform);
            }

            SceneView.RepaintAll();
            return true;
        }

        public static bool HasPreview(PipesPuzzleManager manager)
        {
            PipesPuzzleManager target = SceneInstance(manager);
            return target != null && FindPreviews(target).Count > 0;
        }

        public static void ClearPreview(PipesPuzzleManager manager)
        {
            PipesPuzzleManager target = SceneInstance(manager);
            if (target == null) return;

            foreach (GameObject preview in FindPreviews(target))
            {
                Object.DestroyImmediate(preview);
            }

            SceneView.RepaintAll();
        }

        private static List<GameObject> FindPreviews(PipesPuzzleManager target)
        {
            return target.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == PreviewName)
                .Select(t => t.gameObject)
                .ToList();
        }

        private static PipesPuzzleManager SceneInstance(PipesPuzzleManager manager)
        {
            if (manager == null) return null;
            if (!EditorUtility.IsPersistent(manager)) return manager;

            string path = AssetDatabase.GetAssetPath(manager);
            PipesPuzzleManager[] open = StageUtility.GetCurrentStageHandle().FindComponentsOfType<PipesPuzzleManager>();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) return open.FirstOrDefault();

            return open.FirstOrDefault(m => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m) == path);
        }

        private static void CopyModel(GameObject prefab, Transform parent)
        {
            PipeTotem totem = prefab.GetComponentInChildren<PipeTotem>(true);
            Transform root = totem != null ? totem.transform : prefab.transform;

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;

                Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;

                GameObject model = new(filter.name) { hideFlags = HideFlags.DontSave };
                model.transform.SetParent(parent, false);
                model.transform.localPosition = toRoot.GetPosition();
                model.transform.localRotation = toRoot.rotation;
                model.transform.localScale = toRoot.lossyScale;

                model.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

                if (filter.TryGetComponent(out MeshRenderer renderer))
                {
                    model.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                }
            }
        }

        // ---------- leitura do PipesManager ----------

        private sealed class ManagerData
        {
            public Transform SpawnList;

            // Filhos do PipesManager fora do SpawnList que têm modelo: canos fixos (ex.: a entrada).
            public List<Transform> Fixed;
            public List<float> Angles;
            public GameObject DefaultPrefab;

            // A grade de referência do SpawnList: Spawn1 + coluna * ColumnStep + linha * RowStep.
            public Vector3 Origin;
            public Vector3 ColumnStep;
            public Vector3 RowStep;

            public Vector3 Reference(int column, int row) => Origin + column * ColumnStep + row * RowStep;
        }

        private static bool TryReadManager(PipesPuzzleManager manager, out ManagerData data, out string error)
        {
            data = null;
            error = null;

            if (manager == null)
            {
                error = "Escolha o PipesManager.";
                return false;
            }

            SerializedObject managerObject = new(manager);
            Transform spawnList = managerObject.FindProperty("spawnListRoot").objectReferenceValue as Transform;
            SerializedProperty angleList = managerObject.FindProperty("possibleAngles");

            List<float> angles = new();
            for (int i = 0; i < angleList.arraySize; i++) angles.Add(angleList.GetArrayElementAtIndex(i).floatValue);

            if (spawnList == null || angles.Count == 0)
            {
                error = "O PipesManager precisa de Spawn List Root e Possible Angles.";
                return false;
            }

            List<Transform> spawns = PipeGridResolver.GetOrderedSpawnPoints(spawnList);

            if (spawns.Count < 2)
            {
                error = "O SpawnList precisa de pelo menos 2 spawns para dar a direção das colunas.";
                return false;
            }

            // Colunas: Spawn1 -> Spawn2. A linha quebra no primeiro spawn que não segue esse passo.
            Vector3 columnStep = spawns[1].localPosition - spawns[0].localPosition;
            int gridColumns = spawns.Count;

            for (int i = 2; i < spawns.Count; i++)
            {
                if ((spawns[i].localPosition - spawns[i - 1].localPosition - columnStep).magnitude > 0.001f)
                {
                    gridColumns = i;
                    break;
                }
            }

            Vector3 rowStep = gridColumns < spawns.Count
                ? spawns[gridColumns].localPosition - spawns[0].localPosition
                : Vector3.Cross(Vector3.right, columnStep);

            List<Transform> fixedPieces = new();

            foreach (Transform child in manager.transform)
            {
                if (child == spawnList || spawnList.IsChildOf(child)) continue;
                if (child.GetComponentInChildren<MeshFilter>(true) != null) fixedPieces.Add(child);
            }

            data = new ManagerData
            {
                SpawnList = spawnList,
                Fixed = fixedPieces,
                Angles = angles,
                DefaultPrefab = managerObject.FindProperty("defaultPipePrefab").objectReferenceValue as GameObject,
                Origin = spawns[0].localPosition,
                ColumnStep = columnStep,
                RowStep = rowStep
            };

            return true;
        }

        private static List<PipeFitFixed> FixedPieces(ManagerData data)
        {
            List<PipeFitFixed> pieces = new();
            Matrix4x4 toSpawnList = data.SpawnList.worldToLocalMatrix;

            foreach (Transform fixedRoot in data.Fixed)
            {
                PipeFitFixed piece = new() { Name = fixedRoot.name };

                foreach (MeshFilter filter in fixedRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;

                    Matrix4x4 toLocal = toSpawnList * filter.transform.localToWorldMatrix;

                    foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    {
                        piece.Vertices.Add(toLocal.MultiplyPoint3x4(vertex));
                    }
                }

                if (piece.Vertices.Count > 0) pieces.Add(piece);
            }

            return pieces;
        }

        private static GameObject PrefabOf(PipeCellData cell, ManagerData data)
        {
            return cell.prefabOverride != null ? cell.prefabOverride : data.DefaultPrefab;
        }

        // Os vértices do modelo no espaço do cano, como ele nasce no spawn.
        private static PipeFitPiece Piece(GameObject prefab, Dictionary<GameObject, PipeFitPiece> cache)
        {
            if (prefab == null) return null;
            if (cache.TryGetValue(prefab, out PipeFitPiece cached)) return cached;

            PipeTotem pipe = prefab.GetComponentInChildren<PipeTotem>(true);
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();

            if (pipe == null || filters.Length == 0)
            {
                cache[prefab] = null;
                return null;
            }

            Transform root = pipe.transform;

            PipeFitPiece piece = new()
            {
                Name = prefab.name,
                RotationAxis = new SerializedObject(pipe).FindProperty("rotationAxis").vector3Value
            };

            foreach (MeshFilter filter in filters)
            {
                Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;

                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    piece.Vertices.Add(toRoot.MultiplyPoint3x4(vertex));
                }
            }

            cache[prefab] = piece;
            return piece;
        }

        // Tudo que muda a resposta do encaixe. As posições salvas não entram (são a resposta).
        private static string Signature(PipeGridLayout layout, PipesPuzzleManager manager)
        {
            if (!TryReadManager(manager, out ManagerData data, out _)) return "sem-manager";

            StringBuilder text = new();

            text.Append(string.Join(",", data.Angles.Select(a => a.ToString("0.###", Invariant))));
            text.Append('|').Append(Guid(data.DefaultPrefab));
            text.Append('|').Append(Round(data.Origin)).Append(Round(data.ColumnStep)).Append(Round(data.RowStep));

            foreach (Transform fixedRoot in data.Fixed)
            {
                Matrix4x4 pose = data.SpawnList.worldToLocalMatrix * fixedRoot.localToWorldMatrix;
                text.Append('|').Append(fixedRoot.name).Append(Round(pose.GetPosition())).Append(Round(pose.rotation.eulerAngles));
            }

            foreach (PipeCellData cell in layout.Cells.OrderBy(c => c.row).ThenBy(c => c.column))
            {
                text.Append('|').Append(cell.column).Append(',').Append(cell.row).Append(',').Append(Guid(cell.prefabOverride));
                text.Append(',').Append(cell.correctSteps != null ? string.Join(".", cell.correctSteps) : "");
            }

            return Hash128.Compute(text.ToString()).ToString();
        }

        private static string Guid(Object asset)
        {
            return asset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)) : "-";
        }

        private static string Round(Vector3 v)
        {
            return $"({v.x.ToString("0.0000", Invariant)};{v.y.ToString("0.0000", Invariant)};{v.z.ToString("0.0000", Invariant)})";
        }

        // Sem cache: só roda quando um grid é selecionado, e assim um PipesManager novo aparece na hora.
        private static List<PipesPuzzleManager> AllManagerPrefabs()
        {
            string scriptPath = AssetDatabase.FindAssets("PipesPuzzleManager t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("/PipesPuzzleManager.cs"));

            List<PipesPuzzleManager> managers = new();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (scriptPath != null && !AssetDatabase.GetDependencies(path, false).Contains(scriptPath)) continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                PipesPuzzleManager manager = prefab != null ? prefab.GetComponent<PipesPuzzleManager>() : null;

                if (manager != null) managers.Add(manager);
            }

            return managers;
        }
    }
}
#endif
