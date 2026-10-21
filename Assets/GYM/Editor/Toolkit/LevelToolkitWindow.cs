using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Toolkit
{
    /// <summary>
    /// The toolkit as a palette: every placeable piece with its code, size and the game rules it
    /// carries. Click to inspect, double-click or drag to drop it in the scene.
    /// </summary>
    public class LevelToolkitWindow : EditorWindow
    {
        private const string PrefsPrefix = "TCC.LevelToolkit.";
        private const float SidebarWidth = 190f;
        private const float DetailWidth = 300f;
        private const int RendersPerTick = 4;

        private LevelToolkitCatalog _catalog;
        private string _search = string.Empty;
        private int _categoryIndex = -1;
        private bool _onlyCollider;
        private bool _onlyBlocksVision;
        private bool _onlyInteractive;
        private bool _parentToSelection;
        private float _cardSize = 128f;
        private bool _showMetrics = true;
        private Vector2 _gridScroll;
        private Vector2 _sideScroll;
        private Vector2 _detailScroll;
        private ToolkitEntry _selected;
        private double _lastClickTime;
        private readonly Queue<ToolkitEntry> _renderQueue = new();
        private readonly HashSet<string> _queued = new();

        private GUIStyle _codeStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _chipStyle;
        private GUIStyle _wrapStyle;

        [MenuItem("Tools/TCC/Level Design/Toolkit (janela)", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<LevelToolkitWindow>();
            window.titleContent = new GUIContent("Level Toolkit", EditorGUIUtility.IconContent("d_Prefab Icon").image);
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _search = EditorPrefs.GetString(PrefsPrefix + "search", string.Empty);
            _categoryIndex = EditorPrefs.GetInt(PrefsPrefix + "category", -1);
            _cardSize = EditorPrefs.GetFloat(PrefsPrefix + "cardSize", 128f);
            _parentToSelection = EditorPrefs.GetBool(PrefsPrefix + "parent", false);
            _catalog = AssetDatabase.LoadAssetAtPath<LevelToolkitCatalog>(LevelToolkitScanner.CatalogPath);
            EditorApplication.update += RenderPending;
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefsPrefix + "search", _search);
            EditorPrefs.SetInt(PrefsPrefix + "category", _categoryIndex);
            EditorPrefs.SetFloat(PrefsPrefix + "cardSize", _cardSize);
            EditorPrefs.SetBool(PrefsPrefix + "parent", _parentToSelection);
            EditorApplication.update -= RenderPending;
        }

        private void BuildStyles()
        {
            if (_codeStyle != null) return;

            _codeStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            _nameStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false, clipping = TextClipping.Clip };
            _chipStyle = new GUIStyle(EditorStyles.miniButton) { fixedHeight = 16f, fontSize = 9, padding = new RectOffset(5, 5, 1, 1) };
            _wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
        }

        private void OnGUI()
        {
            BuildStyles();

            if (_catalog == null || _catalog.entries.Count == 0)
            {
                DrawEmpty();
                return;
            }

            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebar();
                DrawGrid();
                if (_selected != null) DrawDetails();
            }
        }

        private void DrawEmpty()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(360f)))
                {
                    EditorGUILayout.HelpBox("O catálogo ainda não existe. Ele varre Assets/Prefabs, separa as peças por categoria e dá um código para cada uma.", MessageType.Info);
                    if (GUILayout.Button("Gerar catálogo agora", GUILayout.Height(30f)))
                    {
                        _catalog = LevelToolkitScanner.Scan();
                    }
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.FlexibleSpace();
        }

        // ---------------------------------------------------------------- toolbar

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f), GUILayout.MaxWidth(260f));

                _onlyCollider = GUILayout.Toggle(_onlyCollider, "Colisor", EditorStyles.toolbarButton);
                _onlyBlocksVision = GUILayout.Toggle(_onlyBlocksVision, "Bloqueia visão", EditorStyles.toolbarButton);
                _onlyInteractive = GUILayout.Toggle(_onlyInteractive, "Interativo", EditorStyles.toolbarButton);

                GUILayout.Space(8f);
                _parentToSelection = GUILayout.Toggle(_parentToSelection, new GUIContent("Filho da seleção", "Ao colocar na cena, vira filho do objeto selecionado."), EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();
                _cardSize = GUILayout.HorizontalSlider(_cardSize, 80f, 200f, GUILayout.Width(90f));

                if (GUILayout.Button(new GUIContent("Atualizar", "Varre os prefabs de novo. Códigos e notas existentes são mantidos."), EditorStyles.toolbarButton))
                {
                    _catalog = LevelToolkitScanner.Scan();
                }

                if (GUILayout.Button(new GUIContent("Exportar", "Gera Docs/LevelDesign/toolkit (JSON + miniaturas) e o Markdown do catálogo."), EditorStyles.toolbarButton))
                {
                    LevelToolkitScanner.Export(_catalog);
                }

                if (GUILayout.Button(new GUIContent("↻ Miniaturas", "Renderiza todas as miniaturas de novo."), EditorStyles.toolbarButton))
                {
                    ToolkitThumbnails.ClearCache();
                    _queued.Clear();
                    _renderQueue.Clear();
                }
            }
        }

        // ---------------------------------------------------------------- sidebar

        private void DrawSidebar()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_sideScroll, GUILayout.Width(SidebarWidth)))
            {
                _sideScroll = scroll.scrollPosition;

                if (CategoryButton("Todas", _catalog.entries.Count, _categoryIndex < 0)) _categoryIndex = -1;

                for (int i = 0; i < CategoryInfo.All.Length; i++)
                {
                    var category = CategoryInfo.All[i];
                    int count = _catalog.entries.Count(e => e.category == category);
                    if (count == 0) continue;

                    if (CategoryButton($"{CategoryInfo.Prefix(category)}  {CategoryInfo.Label(category)}", count, _categoryIndex == i))
                    {
                        _categoryIndex = i;
                    }
                }

                GUILayout.Space(6f);

                if (_categoryIndex >= 0)
                {
                    EditorGUILayout.LabelField(CategoryInfo.Purpose(CategoryInfo.All[_categoryIndex]), EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Space(6f);
                }

                _showMetrics = EditorGUILayout.Foldout(_showMetrics, "Métricas do jogo", true);
                if (_showMetrics)
                {
                    EditorGUILayout.LabelField(_catalog.metricsSnapshot ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField($"Varredura: {_catalog.lastScan}", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private static bool CategoryButton(string label, int count, bool active)
        {
            var style = new GUIStyle(active ? EditorStyles.toolbarButton : EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                fixedHeight = 20f,
            };

            return GUILayout.Button($"{label}  ({count})", style);
        }

        // ---------------------------------------------------------------- grid

        private IEnumerable<ToolkitEntry> Filtered()
        {
            IEnumerable<ToolkitEntry> list = _catalog.entries.Where(e => e.prefab != null);

            if (_categoryIndex >= 0) list = list.Where(e => e.category == CategoryInfo.All[_categoryIndex]);
            if (_onlyCollider) list = list.Where(e => e.Has(ToolkitFlags.HasCollider));
            if (_onlyBlocksVision) list = list.Where(e => e.Has(ToolkitFlags.BlocksMonsterVision));
            if (_onlyInteractive) list = list.Where(e => e.Has(ToolkitFlags.Interactive));

            if (!string.IsNullOrWhiteSpace(_search))
            {
                string term = _search.Trim().ToLowerInvariant();
                list = list.Where(e =>
                    e.code.ToLowerInvariant().Contains(term) ||
                    e.displayName.ToLowerInvariant().Contains(term) ||
                    (e.theme ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (e.notes ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            return list;
        }

        private void DrawGrid()
        {
            var entries = Filtered().ToList();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_gridScroll))
            {
                _gridScroll = scroll.scrollPosition;

                float available = position.width - SidebarWidth - (_selected != null ? DetailWidth : 0f) - 24f;
                float cardWidth = _cardSize + 8f;
                int columns = Mathf.Max(1, Mathf.FloorToInt(available / cardWidth));
                float cardHeight = _cardSize + 40f;

                if (entries.Count == 0)
                {
                    EditorGUILayout.HelpBox("Nada encontrado com esses filtros.", MessageType.None);
                }

                for (int i = 0; i < entries.Count; i += columns)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int c = 0; c < columns && i + c < entries.Count; c++)
                        {
                            var rect = GUILayoutUtility.GetRect(cardWidth, cardHeight, GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));
                            DrawCard(rect, entries[i + c]);
                        }

                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private void DrawCard(Rect rect, ToolkitEntry entry)
        {
            var inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            bool selected = entry == _selected;

            EditorGUI.DrawRect(inner, selected ? new Color(0.24f, 0.42f, 0.66f, 0.55f) : new Color(0f, 0f, 0f, 0.18f));

            var thumbRect = new Rect(inner.x + 2f, inner.y + 2f, inner.width - 4f, inner.width - 4f);

            // Only cards actually on screen ask for a render.
            Texture texture = ToolkitThumbnails.Cached(entry);
            if (texture == null)
            {
                Enqueue(entry);
                texture = AssetPreview.GetMiniThumbnail(entry.prefab);
            }

            if (texture != null) GUI.DrawTexture(thumbRect, texture, ScaleMode.ScaleToFit);

            DrawBadges(thumbRect, entry);

            var codeRect = new Rect(inner.x + 4f, thumbRect.yMax + 1f, inner.width - 8f, 16f);
            GUI.Label(codeRect, entry.code, _codeStyle);
            GUI.Label(new Rect(codeRect.x, codeRect.yMax - 2f, codeRect.width, 14f), entry.displayName, _nameStyle);
            GUI.Label(new Rect(codeRect.x, codeRect.yMax + 10f, codeRect.width, 14f), LevelToolkitScanner.FormatSize(entry.size), EditorStyles.centeredGreyMiniLabel);

            HandleCardEvents(rect, entry);
        }

        private static void DrawBadges(Rect thumb, ToolkitEntry entry)
        {
            var badges = new List<(string, Color)>();
            if (entry.Has(ToolkitFlags.Interactive)) badges.Add(("I", new Color(0.95f, 0.75f, 0.2f)));
            if (entry.Has(ToolkitFlags.BlocksMonsterVision)) badges.Add(("V", new Color(0.4f, 0.75f, 0.45f)));
            if (entry.Has(ToolkitFlags.EmitsLight)) badges.Add(("L", new Color(1f, 0.95f, 0.6f)));
            if (entry.Has(ToolkitFlags.NeedsNetworkParent)) badges.Add(("!", new Color(0.95f, 0.35f, 0.3f)));
            if (!entry.Has(ToolkitFlags.HasCollider) && entry.category is ToolkitCategory.PAR_Parede or ToolkitCategory.PIS_Piso)
                badges.Add(("∅", new Color(0.7f, 0.7f, 0.75f)));

            for (int i = 0; i < badges.Count; i++)
            {
                var r = new Rect(thumb.xMax - 16f - i * 17f, thumb.y + 2f, 15f, 15f);
                EditorGUI.DrawRect(r, badges[i].Item2);
                GUI.Label(r, badges[i].Item1, new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } });
            }
        }

        private void HandleCardEvents(Rect rect, ToolkitEntry entry)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                bool doubleClick = _selected == entry && EditorApplication.timeSinceStartup - _lastClickTime < 0.35;
                _lastClickTime = EditorApplication.timeSinceStartup;
                _selected = entry;

                if (doubleClick) Place(entry);

                GUI.FocusControl(null);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && _selected == entry)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { entry.prefab };
                DragAndDrop.StartDrag(entry.code);
                e.Use();
            }
            else if (e.type == EventType.ContextClick)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Colocar na cena"), false, () => Place(entry));
                menu.AddItem(new GUIContent("Copiar código"), false, () => EditorGUIUtility.systemCopyBuffer = entry.code);
                menu.AddItem(new GUIContent("Mostrar no Project"), false, () => EditorGUIUtility.PingObject(entry.prefab));
                menu.AddItem(new GUIContent("Abrir prefab"), false, () => AssetDatabase.OpenAsset(entry.prefab));
                menu.ShowAsContext();
                e.Use();
            }
        }

        // ---------------------------------------------------------------- details

        private void DrawDetails()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll, EditorStyles.helpBox, GUILayout.Width(DetailWidth)))
            {
                _detailScroll = scroll.scrollPosition;
                var e = _selected;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(e.code, new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 }, GUILayout.Height(22f));
                    if (GUILayout.Button("×", GUILayout.Width(22f))) { _selected = null; return; }
                }

                EditorGUILayout.LabelField(e.displayName, EditorStyles.largeLabel);

                var texture = ToolkitThumbnails.Cached(e);
                if (texture != null)
                {
                    var rect = GUILayoutUtility.GetRect(DetailWidth - 20f, DetailWidth - 20f);
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                }

                EditorGUILayout.LabelField($"{CategoryInfo.Label(e.category)} · {e.theme}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"Tamanho: {LevelToolkitScanner.FormatSize(e.size)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Layer: {e.layer}", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(e.assetPath, EditorStyles.miniLabel, GUILayout.Height(14f));

                DrawChips(e);

                if (!string.IsNullOrEmpty(e.components))
                {
                    EditorGUILayout.LabelField("Scripts: " + e.components, EditorStyles.wordWrappedMiniLabel);
                }

                if (!string.IsNullOrEmpty(e.autoNotes))
                {
                    EditorGUILayout.HelpBox(e.autoNotes, MessageType.None);
                }

                EditorGUILayout.LabelField("Minhas notas (Library)", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                string notes = EditorGUILayout.TextArea(e.notes ?? string.Empty, _wrapStyle, GUILayout.MinHeight(54f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_catalog, "Nota do toolkit");
                    e.notes = notes;
                    EditorUtility.SetDirty(_catalog);
                }

                EditorGUI.BeginChangeCheck();
                bool locked = EditorGUILayout.ToggleLeft("Travar categoria (o scanner não muda)", e.lockCategory);
                var category = (ToolkitCategory)EditorGUILayout.EnumPopup("Categoria", e.category);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_catalog, "Categoria do toolkit");
                    e.lockCategory = locked || category != e.category;
                    e.category = category;
                    EditorUtility.SetDirty(_catalog);
                }

                GUILayout.Space(6f);

                if (GUILayout.Button("Colocar na cena", GUILayout.Height(26f))) Place(e);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Mostrar")) EditorGUIUtility.PingObject(e.prefab);
                    if (GUILayout.Button("Abrir")) AssetDatabase.OpenAsset(e.prefab);
                    if (GUILayout.Button("Copiar código")) EditorGUIUtility.systemCopyBuffer = e.code;
                }
            }
        }

        private void DrawChips(ToolkitEntry e)
        {
            var flags = LevelToolkitScanner.EachFlag(e.flags).ToList();
            if (flags.Count == 0) return;

            float width = 0f;
            float max = DetailWidth - 30f;
            EditorGUILayout.BeginHorizontal();

            foreach (var flag in flags)
            {
                var content = new GUIContent(LevelToolkitScanner.FlagLabel(flag));
                float w = _chipStyle.CalcSize(content).x;

                if (width + w > max)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    width = 0f;
                }

                GUILayout.Label(content, _chipStyle);
                width += w + 4f;
            }

            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- placing

        private void Place(ToolkitEntry entry)
        {
            if (entry?.prefab == null) return;

            var view = SceneView.lastActiveSceneView;
            Vector3 point = view != null ? view.pivot : Vector3.zero;
            point = new Vector3(Mathf.Round(point.x * 2f) / 2f, Mathf.Round(point.y * 2f) / 2f, Mathf.Round(point.z * 2f) / 2f);

            Transform parent = _parentToSelection && Selection.activeTransform != null && Selection.activeTransform.gameObject.scene.IsValid()
                ? Selection.activeTransform
                : null;

            var instance = parent != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, parent)
                : (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);

            instance.transform.position = point;
            Undo.RegisterCreatedObjectUndo(instance, $"Colocar {entry.code}");

            // Same rule DoorNetworkSetup applies: a NetworkBehaviour with no NetworkObject above it never spawns.
            if (entry.Has(ToolkitFlags.NeedsNetworkParent) && instance.GetComponentInParent<NetworkObject>(true) == null)
            {
                Undo.AddComponent<NetworkObject>(instance);
                Debug.Log($"LevelToolkit: {entry.code} recebeu um NetworkObject (não havia nenhum acima dele).", instance);
            }

            Selection.activeGameObject = instance;
            if (view != null) view.Focus();
        }

        // ---------------------------------------------------------------- thumbnails

        private void Enqueue(ToolkitEntry entry)
        {
            if (entry == null || !_queued.Add(entry.guid)) return;
            _renderQueue.Enqueue(entry);
        }

        private void RenderPending()
        {
            if (_renderQueue.Count == 0) return;

            for (int i = 0; i < RendersPerTick && _renderQueue.Count > 0; i++)
            {
                ToolkitThumbnails.Get(_renderQueue.Dequeue());
            }

            Repaint();
        }
    }
}
