using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Toolkit
{
    /// <summary>
    /// Walks Assets/Prefabs, sorts every placeable prefab into a toolkit category and hands it a
    /// short code (PAR-04, POR-02...) you can write on a blockout, a beat chart or a Library note.
    ///
    /// <para>Codes are sticky: a rescan keeps the code of every prefab it already knows (matched
    /// by GUID) and only numbers the new ones, so notes that mention "COB-03" stay true. Your
    /// own notes and locked categories are never overwritten.</para>
    /// </summary>
    public static class LevelToolkitScanner
    {
        public const string CatalogPath = "Assets/LevelDesign/Toolkit/LevelToolkitCatalog.asset";
        private const string PrefabRoot = "Assets/Prefabs/";
        private const string MenuRoot = "Tools/TCC/Level Design/";

        private static readonly string[] ExcludedPaths =
        {
            "Assets/Prefabs/Ui/",
            "Assets/Prefabs/Network/",
            "Assets/Prefabs/Player/",
            "/Backup/",
            "Assets/Prefabs/PrototypeMap.prefab",
        };

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string ExportDirectory => Path.Combine(ProjectRoot, "Docs", "LevelDesign", "toolkit");

        [MenuItem(MenuRoot + "Atualizar catálogo do toolkit", priority = 10)]
        public static void ScanMenu()
        {
            var catalog = Scan();
            Selection.activeObject = catalog;
            Debug.Log($"LevelToolkit: {catalog.entries.Count} peças catalogadas em {CatalogPath}.", catalog);
        }

        [MenuItem(MenuRoot + "Exportar toolkit (JSON + miniaturas + Markdown)", priority = 11)]
        public static void ExportMenu()
        {
            var catalog = Scan();
            Export(catalog);
            EditorUtility.RevealInFinder(ExportDirectory);
        }

        public static LevelToolkitCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelToolkitCatalog>(CatalogPath);
            if (catalog != null) return catalog;

            EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            catalog = ScriptableObject.CreateInstance<LevelToolkitCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        public static LevelToolkitCatalog Scan()
        {
            var catalog = LoadOrCreateCatalog();
            var metrics = GameMetrics.Read();
            var known = catalog.entries.Where(e => !string.IsNullOrEmpty(e.guid)).ToDictionary(e => e.guid);
            var scanned = new List<ToolkitEntry>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot.TrimEnd('/') });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ExcludedPaths.Any(path.Contains)) continue;

                EditorUtility.DisplayProgressBar("Level Toolkit", path, (float)i / guids.Length);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                known.TryGetValue(guids[i], out var previous);
                scanned.Add(Describe(prefab, guids[i], path, metrics, previous));
            }

            EditorUtility.ClearProgressBar();

            AssignCodes(scanned);

            catalog.entries = scanned
                .OrderBy(e => e.category)
                .ThenBy(e => CodeNumber(e.code))
                .ToList();
            catalog.metricsSnapshot = metrics.ToReadableText();
            catalog.lastScan = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return catalog;
        }

        private static ToolkitEntry Describe(GameObject prefab, string guid, string path, GameMetrics metrics, ToolkitEntry previous)
        {
            var entry = previous ?? new ToolkitEntry();
            string relative = path.Substring(PrefabRoot.Length);

            entry.guid = guid;
            entry.prefab = prefab;
            entry.assetPath = path;
            entry.displayName = Prettify(prefab.name);
            entry.theme = ThemeOf(relative);
            entry.size = RendererBounds(prefab).size;
            entry.layer = LayerMask.LayerToName(prefab.layer);
            entry.flags = ReadFlags(prefab, metrics);
            entry.components = string.Join(", ", GameplayComponents(prefab));

            if (!entry.lockCategory || previous == null)
            {
                entry.category = Classify(relative, prefab.name, entry, metrics);
            }

            entry.autoNotes = AutoNotes(entry, metrics);
            return entry;
        }

        // ---------------------------------------------------------------- classification

        private static ToolkitCategory Classify(string relative, string name, ToolkitEntry entry, GameMetrics metrics)
        {
            // Whole words only: "Flight_Case" is not a light and "Popcorn_Paper" is not a poster.
            string[] tokens = name.ToLowerInvariant()
                .Split(new[] { '_', ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t != "sm")
                .ToArray();
            string first = tokens.FirstOrDefault() ?? string.Empty;
            bool InFolder(string folder) => relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
            bool Has(params string[] words) => words.Any(w => tokens.Any(t => t.StartsWith(w)));
            // Managers and items are CamelCase (MainDoorEscape), so they are matched inside the word.
            string lower = name.ToLowerInvariant();
            bool Contains(params string[] words) => words.Any(lower.Contains);

            if (InFolder("Rooms/") && relative.Count(c => c == '/') == 1) return ToolkitCategory.SAL_SalaPronta;
            if (InFolder("Rooms/Fixed/") || InFolder("Rooms/Missions/") || InFolder("Rooms/Loot/")) return ToolkitCategory.SAL_SalaPronta;
            if (relative.Contains("Walls_To_Mount/Hallway_")) return ToolkitCategory.COR_ModuloCorredor;
            if (relative.Contains("Walls_To_Mount/Floor_")) return ToolkitCategory.PIS_Piso;
            if (relative.Contains("Walls_To_Mount/Roof")) return ToolkitCategory.TET_Teto;
            if (InFolder("Rooms/Props/Doors/")) return ToolkitCategory.POR_Porta;
            if (InFolder("Audience_Objects/")) return ToolkitCategory.AUD_Audiencia;
            if (InFolder("Missions/Contracts/")) return ToolkitCategory.SIN_Sinalizacao;
            if (InFolder("Items/")) return Contains("eletriccircuit") ? ToolkitCategory.MIS_MissaoPuzzle : ToolkitCategory.ITM_Item;
            if (InFolder("Missions/")) return ToolkitCategory.MIS_MissaoPuzzle;

            if (InFolder("Managers/"))
            {
                if (Contains("door")) return ToolkitCategory.POR_Porta;
                if (Contains("floppy", "totemready", "readyboard", "escapetrigger")) return ToolkitCategory.MIS_MissaoPuzzle;
                if (Contains("light")) return ToolkitCategory.ILU_Iluminacao;
                return ToolkitCategory.SIS_Sistema;
            }

            if (InFolder("Sound/") || InFolder("Monster/")) return ToolkitCategory.SIS_Sistema;
            if (Has("electric")) return ToolkitCategory.MIS_MissaoPuzzle;

            if (InFolder("Rooms/Props/Stage/") && (first == "stage" || Has("arch", "curtain", "auditorium", "truss", "layher")))
                return ToolkitCategory.PAL_PalcoAuditorio;
            if (InFolder("Rooms/Props/Auditorium/") && Has("chair")) return ToolkitCategory.PAL_PalcoAuditorio;

            if (first is "paper" or "pin" or "post" or "poster" || Has("sign", "notice", "menu", "exit"))
                return ToolkitCategory.SIN_Sinalizacao;
            if (Has("light", "chandelier", "lamp", "spotilight")) return ToolkitCategory.ILU_Iluminacao;
            if (Has("floor") && !Has("cabinet", "soundbox")) return ToolkitCategory.PIS_Piso;
            if (Has("roof")) return ToolkitCategory.TET_Teto;
            if (Has("wall") && !Has("cabinet")) return ToolkitCategory.PAR_Parede;

            if (entry.Has(ToolkitFlags.HasCollider))
            {
                float thinnest = Mathf.Min(entry.size.x, entry.size.z);
                float widest = Mathf.Max(entry.size.x, entry.size.z);
                float largest = Mathf.Max(widest, entry.size.y);

                // Cups, hats, flat frames: you walk past them, they shape nothing.
                if (thinnest < 0.15f || largest < 0.45f) return ToolkitCategory.DEC_Decoracao;

                // Tall and either deep or wide (a locker, a folding screen) — not a mic stand.
                bool hidesStanding = entry.size.y >= metrics.standHeight && (thinnest >= 0.4f || widest >= 1f);
                return hidesStanding ? ToolkitCategory.COB_CoberturaAlta : ToolkitCategory.OBS_ObstaculoBaixo;
            }

            return ToolkitCategory.DEC_Decoracao;
        }

        private static string ThemeOf(string relative)
        {
            if (relative.Contains("/Hallway/")) return "Corredor";
            if (relative.Contains("/Bathroom/")) return "Banheiro";
            if (relative.Contains("/Box_Office/")) return "Bilheteria";
            if (relative.Contains("/Dressing_Room/")) return "Camarim";
            if (relative.Contains("/Stage/")) return "Palco";
            if (relative.Contains("/Auditorium/")) return "Auditório";
            if (relative.Contains("/Other_Rooms/")) return "Depósito";
            if (relative.Contains("/Doors/")) return "Portas";
            if (relative.StartsWith("Rooms/Missions/")) return "Sala de missão";
            if (relative.StartsWith("Rooms/Fixed/")) return "Sala fixa";
            if (relative.StartsWith("Rooms/Loot/")) return "Sala de loot";
            if (relative.StartsWith("Rooms/")) return "Sala especial";
            if (relative.StartsWith("Missions/")) return "Missões";
            if (relative.StartsWith("Items/")) return "Itens";
            if (relative.StartsWith("Audience_Objects/")) return "Audiência";
            if (relative.StartsWith("Managers/")) return "Managers";
            if (relative.StartsWith("Sound/")) return "Som";
            if (relative.StartsWith("Monster/")) return "Monstro";
            return "Geral";
        }

        // ---------------------------------------------------------------- flags & notes

        private static ToolkitFlags ReadFlags(GameObject prefab, GameMetrics metrics)
        {
            var flags = ToolkitFlags.None;
            var components = prefab.GetComponentsInChildren<Component>(true).Where(c => c != null).ToArray();

            foreach (var collider in components.OfType<Collider>().Where(c => !c.isTrigger))
            {
                int bit = 1 << collider.gameObject.layer;
                flags |= ToolkitFlags.HasCollider;
                if ((metrics.monsterOcclusionMask & bit) != 0) flags |= ToolkitFlags.BlocksMonsterVision;
                if ((metrics.stepMask & bit) != 0) flags |= ToolkitFlags.StepClimbable;
                if ((metrics.crouchCeilingMask & bit) != 0) flags |= ToolkitFlags.CrouchCeiling;
            }

            bool hasNetworkObject = false, hasNetworkBehaviour = false;

            foreach (var component in components)
            {
                var type = component.GetType();
                string typeName = type.Name;

                if (type.GetInterfaces().Any(i => i.Name is "IInteractable" or "IUsable")) flags |= ToolkitFlags.Interactive;
                if (typeName == "NetworkObject") hasNetworkObject = true;
                else if (DerivesFrom(type, "NetworkBehaviour")) hasNetworkBehaviour = true;
                if (component is Light) flags |= ToolkitFlags.EmitsLight;
                if (typeName.Contains("Sabotage")) flags |= ToolkitFlags.Sabotable;
                if (typeName == "RecordableIdentifier") flags |= ToolkitFlags.Recordable;
                if (component is Rigidbody body && !body.isKinematic) flags |= ToolkitFlags.Physics;
                if (component is UnityEngine.AI.NavMeshObstacle) flags |= ToolkitFlags.NavMeshObstacle;
                if (typeName == "NoiseEmitter") flags |= ToolkitFlags.MakesNoise;
            }

            if (hasNetworkObject) flags |= ToolkitFlags.Networked;
            else if (hasNetworkBehaviour) flags |= ToolkitFlags.NeedsNetworkParent;

            return flags;
        }

        private static string AutoNotes(ToolkitEntry e, GameMetrics m)
        {
            var notes = new List<string>();
            bool structural = e.category is ToolkitCategory.PAR_Parede or ToolkitCategory.PIS_Piso or ToolkitCategory.COR_ModuloCorredor;

            if (structural && !e.Has(ToolkitFlags.HasCollider))
                notes.Add(e.category == ToolkitCategory.PIS_Piso
                    ? "Sem colisor: o chão precisa de um BoxCollider separado na layer Ground."
                    : "Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls).");

            if (e.Has(ToolkitFlags.HasCollider) && !e.Has(ToolkitFlags.BlocksMonsterVision) && e.size.y >= m.crouchHeight)
                notes.Add($"Colisor na layer {e.layer}: NÃO bloqueia a visão do monstro.");

            if (e.category is ToolkitCategory.COB_CoberturaAlta or ToolkitCategory.OBS_ObstaculoBaixo)
            {
                if (e.size.y >= m.standHeight) notes.Add($"Esconde o jogador em pé ({m.standHeight:0.##} m).");
                else if (e.size.y >= m.crouchHeight) notes.Add($"Esconde só quem está agachado ({m.crouchHeight:0.##} m).");
                else notes.Add("Baixo demais para esconder; atrapalha a rota.");
            }

            if (e.Has(ToolkitFlags.StepClimbable) && e.size.y <= m.maxStepHeight + 0.01f)
                notes.Add($"Degrau: sobe sozinho (até {m.maxStepHeight:0.##} m na layer Ground).");

            if (e.Has(ToolkitFlags.NeedsNetworkParent))
                notes.Add("Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede.");

            if (e.Has(ToolkitFlags.Interactive)) notes.Add($"Interagível a até {m.interactDistance:0.##} m.");
            if (e.Has(ToolkitFlags.Sabotable)) notes.Add("O monstro pode sabotar.");
            if (e.Has(ToolkitFlags.Recordable)) notes.Add("Dá audiência quando filmado.");
            if (e.category == ToolkitCategory.POR_Porta && m.doorLockSeconds > 0f)
                notes.Add($"Segura o monstro ~{m.doorLockSeconds:0.#} s quando ele força.");

            return string.Join(" ", notes);
        }

        public static string FlagLabel(ToolkitFlags flag) => flag switch
        {
            ToolkitFlags.HasCollider => "Colisor",
            ToolkitFlags.BlocksMonsterVision => "Bloqueia visão",
            ToolkitFlags.StepClimbable => "Piso/degrau",
            ToolkitFlags.CrouchCeiling => "Teto p/ agachar",
            ToolkitFlags.Interactive => "Interativo",
            ToolkitFlags.Networked => "Rede",
            ToolkitFlags.NeedsNetworkParent => "Precisa NetworkObject",
            ToolkitFlags.EmitsLight => "Luz",
            ToolkitFlags.Sabotable => "Sabotável",
            ToolkitFlags.Recordable => "Audiência",
            ToolkitFlags.Physics => "Física",
            ToolkitFlags.NavMeshObstacle => "Obstáculo NavMesh",
            ToolkitFlags.MakesNoise => "Faz barulho",
            _ => flag.ToString(),
        };

        public static IEnumerable<ToolkitFlags> EachFlag(ToolkitFlags flags) =>
            Enum.GetValues(typeof(ToolkitFlags)).Cast<ToolkitFlags>().Where(f => f != ToolkitFlags.None && (flags & f) == f);

        // ---------------------------------------------------------------- codes

        private static void AssignCodes(List<ToolkitEntry> entries)
        {
            foreach (var group in entries.GroupBy(e => e.category))
            {
                string prefix = CategoryInfo.Prefix(group.Key);

                // A recategorised piece gives its old code up and gets one from its new family.
                foreach (var entry in group.Where(e => e.code != null && !e.code.StartsWith(prefix + "-")))
                {
                    entry.code = null;
                }

                int next = group.Select(e => CodeNumber(e.code)).DefaultIfEmpty(0).Max() + 1;

                foreach (var entry in group.Where(e => string.IsNullOrEmpty(e.code)).OrderBy(e => e.theme).ThenBy(e => e.displayName))
                {
                    entry.code = $"{prefix}-{next++:00}";
                }
            }
        }

        private static int CodeNumber(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0;
            int dash = code.IndexOf('-');
            return dash >= 0 && int.TryParse(code.Substring(dash + 1), out int number) ? number : 0;
        }

        // ---------------------------------------------------------------- helpers

        public static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;

            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        public static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is MeshRenderer or SkinnedMeshRenderer)
                .ToArray();

            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static IEnumerable<string> GameplayComponents(GameObject prefab)
        {
            return prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c != null)
                .Select(c => c.GetType())
                .Where(t => !t.Namespace?.StartsWith("UnityEngine") ?? true)
                .Where(t => !t.Namespace?.StartsWith("TMPro") ?? true)
                .Where(t => !t.Namespace?.StartsWith("Unity.") ?? true)
                .Select(t => t.Name)
                .Where(n => n is not ("Outline" or "Highlight" or "InteractionMarker"))
                .Distinct()
                .OrderBy(n => n);
        }

        private static bool DerivesFrom(Type type, string baseName)
        {
            for (var t = type.BaseType; t != null; t = t.BaseType)
            {
                if (t.Name == baseName) return true;
            }

            return false;
        }

        public static string Prettify(string prefabName)
        {
            string name = prefabName.StartsWith("SM_") ? prefabName.Substring(3) : prefabName;
            return name.Replace('_', ' ').Trim();
        }

        public static string FormatSize(Vector3 size) =>
            size == Vector3.zero
                ? "—"
                : string.Format(CultureInfo.GetCultureInfo("pt-BR"), "{0:0.0} × {1:0.0} × {2:0.0} m", size.x, size.y, size.z);

        // ---------------------------------------------------------------- export

        [Serializable]
        private class ExportEntry
        {
            public string code;
            public string name;
            public string prefab;
            public string category;
            public string categoryLabel;
            public string theme;
            public string path;
            public float[] size;
            public string layer;
            public string[] flags;
            public string components;
            public string autoNotes;
            public string notes;
            public string thumb;
        }

        [Serializable]
        private class ExportCategory
        {
            public string prefix;
            public string label;
            public string purpose;
            public int count;
        }

        [Serializable]
        private class ExportRoot
        {
            public string generatedAt;
            public GameMetrics metrics;
            public string metricsText;
            public ExportCategory[] categories;
            public ExportEntry[] entries;
        }

        public static void Export(LevelToolkitCatalog catalog)
        {
            string thumbs = Path.Combine(ExportDirectory, "thumbs");
            Directory.CreateDirectory(thumbs);

            var metrics = GameMetrics.Read();
            var entries = new List<ExportEntry>();

            for (int i = 0; i < catalog.entries.Count; i++)
            {
                var e = catalog.entries[i];
                EditorUtility.DisplayProgressBar("Level Toolkit", $"Miniatura {e.code}", (float)i / catalog.entries.Count);

                string thumbFile = null;
                var texture = ToolkitThumbnails.Get(e, force: true);
                if (texture != null)
                {
                    thumbFile = e.code + ".jpg";
                    File.WriteAllBytes(Path.Combine(thumbs, thumbFile), texture.EncodeToJPG(85));
                }

                entries.Add(new ExportEntry
                {
                    code = e.code,
                    name = e.displayName,
                    prefab = e.prefab != null ? e.prefab.name : string.Empty,
                    category = CategoryInfo.Prefix(e.category),
                    categoryLabel = CategoryInfo.Label(e.category),
                    theme = e.theme,
                    path = e.assetPath,
                    size = new[] { Round(e.size.x), Round(e.size.y), Round(e.size.z) },
                    layer = e.layer,
                    flags = EachFlag(e.flags).Select(FlagLabel).ToArray(),
                    components = e.components,
                    autoNotes = e.autoNotes,
                    notes = e.notes,
                    thumb = thumbFile,
                });
            }

            EditorUtility.ClearProgressBar();

            var root = new ExportRoot
            {
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                metrics = metrics,
                metricsText = metrics.ToReadableText(),
                categories = CategoryInfo.All.Select(c => new ExportCategory
                {
                    prefix = CategoryInfo.Prefix(c),
                    label = CategoryInfo.Label(c),
                    purpose = CategoryInfo.Purpose(c),
                    count = catalog.entries.Count(e => e.category == c),
                }).ToArray(),
                entries = entries.ToArray(),
            };

            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(ExportDirectory, "toolkit.json"), JsonUtility.ToJson(root, true), utf8);
            File.WriteAllText(Path.Combine(ExportDirectory, "..", "02-Toolkit-Catalogo.md"), BuildMarkdown(catalog, metrics), utf8);
            WritePage(JsonUtility.ToJson(root), utf8);

            Debug.Log($"LevelToolkit: exportado em {ExportDirectory} ({entries.Count} peças).");
        }

        private static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        /// <summary>
        /// The visual catalog (Docs/LevelDesign/toolkit/index.html): opens offline in any browser,
        /// thumbnails read from the thumbs folder next to it.
        /// </summary>
        private static void WritePage(string json, Encoding encoding)
        {
            string template = Path.Combine(ProjectRoot, "Assets", "LevelDesign", "Editor", "Toolkit", "ToolkitPageTemplate.html");
            if (!File.Exists(template)) return;

            string body = File.ReadAllText(template)
                .Replace("__TOOLKIT_DATA__", json.Replace("</", "<\\/"))
                .Replace("__TOOLKIT_THUMBS__", "{}");

            string page = "<!doctype html>\n<html lang=\"pt-BR\">\n<head>\n<meta charset=\"utf-8\">\n" +
                          "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\">\n" +
                          body + "\n</html>\n";

            File.WriteAllText(Path.Combine(ExportDirectory, "index.html"), page, encoding);
        }

        private static string BuildMarkdown(LevelToolkitCatalog catalog, GameMetrics metrics)
        {
            var md = new StringBuilder();
            md.AppendLine("# Toolkit de Level Design — catálogo");
            md.AppendLine();
            md.AppendLine($"> Gerado por `{MenuRoot}Exportar toolkit` em {catalog.lastScan}. Não edite à mão: rode o menu de novo.");
            md.AppendLine("> Suas anotações ficam no campo **notes** do asset `LevelToolkitCatalog` e saem aqui na coluna Notas.");
            md.AppendLine();
            md.AppendLine("## Métricas do jogo (lidas dos prefabs)");
            md.AppendLine();
            md.AppendLine("```");
            md.AppendLine(metrics.ToReadableText());
            md.AppendLine("```");
            md.AppendLine();

            foreach (var category in CategoryInfo.All)
            {
                var list = catalog.entries.Where(e => e.category == category).ToList();
                if (list.Count == 0) continue;

                md.AppendLine($"## {CategoryInfo.Prefix(category)} — {CategoryInfo.Label(category)} ({list.Count})");
                md.AppendLine();
                md.AppendLine(CategoryInfo.Purpose(category));
                md.AppendLine();
                md.AppendLine("| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |");
                md.AppendLine("|---|---|---|---|---|---|---|");

                foreach (var e in list)
                {
                    string flags = string.Join(", ", EachFlag(e.flags).Select(FlagLabel));
                    string notes = string.Join(" ", new[] { e.autoNotes, e.notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    md.AppendLine($"| `{e.code}` | {e.displayName} | {e.theme} | {FormatSize(e.size)} | {e.layer} | {flags} | {notes.Replace("|", "/")} |");
                }

                md.AppendLine();
            }

            return md.ToString();
        }
    }
}
