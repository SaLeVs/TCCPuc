using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// Makes Assets/Scenes/Prototypes/GameN from the level drawn in
    /// Docs/LevelDesign/mapas/prototipos/nivel-N.png. The scene starts as a copy of Game, so every
    /// manager and the references between them come along; the old map is removed and the new one
    /// built around the moved hub rooms.
    /// </summary>
    public static class PrototypeBuilder
    {
        public const string SceneFolder = "Assets/Scenes/Prototypes";
        public const string DiagramFolder = "Docs/LevelDesign/mapas/prototipos";
        private const string GameScene = "Assets/Scenes/Game.unity";

        private static readonly string[] OldMap =
        {
            "Floor", "Coliders Floor", "Coliders Walls", "CollidersRoof", "Roof", "Hallway", "Lights", "Room_07 1 (1)",
            "RoomBase_Decoration_1", "VoiceZoneTrigger",
        };
        private static readonly string[] OldMapPrefixes = { "SmallHallway", "BaseRoom_Mission", "FlashlightBatteryPickable", "ObjectAudience" };

        public static string DiagramPath(int level) => $"{DiagramFolder}/nivel-{level}.png";
        public static string ScenePath(int level) => $"{SceneFolder}/Game{level}.unity";

        [MenuItem("Tools/TCC/Protótipos/Gerar nível 1", priority = 1)] private static void M1() => Log(Build(1));
        [MenuItem("Tools/TCC/Protótipos/Gerar nível 2", priority = 2)] private static void M2() => Log(Build(2));
        [MenuItem("Tools/TCC/Protótipos/Gerar nível 3", priority = 3)] private static void M3() => Log(Build(3));
        [MenuItem("Tools/TCC/Protótipos/Gerar nível 4", priority = 4)] private static void M4() => Log(Build(4));
        [MenuItem("Tools/TCC/Protótipos/Gerar nível 5", priority = 5)] private static void M5() => Log(Build(5));
        [MenuItem("Tools/TCC/Protótipos/Gerar nível 6", priority = 6)] private static void M6() => Log(Build(6));
        [MenuItem("Tools/TCC/Protótipos/Gerar nível 7", priority = 7)] private static void M7() => Log(Build(7));

        [MenuItem("Tools/TCC/Protótipos/Gerar todos os níveis", priority = 20)]
        private static void All() { for (int i = 1; i <= 7; i++) Log(Build(i)); }

        private static void Log(string report) => Debug.Log(report);

        public static string Build(int level, bool rebuildAssets = false)
        {
            var report = new StringBuilder($"=== Protótipo nível {level} ===\n");
            var diagram = PrototypeDiagram.Load(DiagramPath(level));
            if (diagram.Problems.Count > 0) return report.Append("planta com problemas:\n" + diagram.Report()).ToString();
            var layout = new PrototypeLayout(diagram);
            if (layout.Problems.Count > 0) return report.Append("layout com problemas:\n" + layout.Report()).ToString();
            report.Append(layout.Report());

            ContractsSO contract = PrototypeAssets.EnsureAll(rebuildAssets);

            string path = ScenePath(level);
            // The level being rebuilt cannot be the only scene open: Unity will not close the last scene,
            // so the new level would be built on top of the old one still in memory.
            if (SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == path)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene previous = SceneManager.GetActiveScene();
            CloseIfOpen(path);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                // Overwritten in place, so the scene keeps its GUID: deleting and copying gave it a new
                // one every time and left the Build Settings entry pointing at the old.
                System.IO.File.Copy(GameScene, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            else AssetDatabase.CopyAsset(GameScene, path);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                var ctx = new Context { Scene = scene, Layout = layout, Level = level, Report = report };
                RemoveOldMap(ctx);
                ctx.Root = new GameObject("NIVEL_PROTOTIPO").transform;
                PlaceHub(ctx);
                PlaceRooms(ctx);

                var corridors = new PrototypeCorridors(layout, ctx.Root);
                corridors.Build();
                ctx.Corridors = corridors;
                foreach (string p in corridors.Problems) report.AppendLine("  PROBLEMA: " + p);
                report.AppendLine($"  escadas: {corridors.Stairs.Count}, dutos: {corridors.Ducts.Count} ({corridors.Ducts.Count(d => d.Tunnel)} túneis), portas no meio de corredor: {corridors.DoorSpots.Count}, luzes: {corridors.LightPoints.Count}");

                FixHubColliders(ctx);
                PrototypeDressing.Dress(ctx);
                PrototypeFoyer.Dress(ctx);
                report.AppendLine(PrototypeTidy.Run(ctx));
                report.AppendLine(PrototypeWallSeal.Run(ctx.Root));
                PlaceSystems(ctx, contract);
                PrototypeLighting.Setup(ctx);
                report.AppendLine(MatchReady(scene));

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AddToBuildSettings(path);
                report.AppendLine("  salvo em " + path);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (previous.path != path) EditorSceneManager.CloseScene(scene, true);
            }
            return report.ToString();
        }

        public class Context
        {
            public Scene Scene;
            public PrototypeLayout Layout;
            public int Level;
            public StringBuilder Report;
            public Transform Root;
            public PrototypeCorridors Corridors;
            public GameObject Auditorium, Bilheteria, Escape, Dressing;
            public readonly List<GameObject> Bathrooms = new List<GameObject>();
            public readonly List<Transform> RoomSlots = new List<Transform>();
            public readonly List<GameObject> ExtraSabotage = new List<GameObject>();

            public GameObject Find(string name) => Scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        }

        private static void CloseIfOpen(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.path == path) EditorSceneManager.CloseScene(s, true);
            }
        }

        private static void RemoveOldMap(Context ctx)
        {
            int removed = 0;
            foreach (GameObject go in ctx.Scene.GetRootGameObjects())
            {
                if (OldMap.Contains(go.name) || OldMapPrefixes.Any(p => go.name.StartsWith(p)))
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
            ctx.Report.AppendLine($"  mapa antigo: {removed} objetos removidos");
        }

        // ------------------------------------------------------------------ hub

        /// <summary>Moves obj so it keeps its pose relative to a parent moving from oldPose to newPose.</summary>
        private static void MoveWith(Transform obj, Vector3 oldPos, Quaternion oldRot, Vector3 newPos, Quaternion newRot)
        {
            Vector3 localPos = Quaternion.Inverse(oldRot) * (obj.position - oldPos);
            Quaternion localRot = Quaternion.Inverse(oldRot) * obj.rotation;
            obj.SetPositionAndRotation(newPos + newRot * localPos, newRot * localRot);
        }

        private static void PlaceHub(Context ctx)
        {
            PrototypeLayout l = ctx.Layout;
            Transform hub = Group("Hub", ctx.Root).transform;

            // Auditorium at the origin; the stage and its two side doors move with it.
            ctx.Auditorium = ctx.Find("Auditorium");
            Vector3 oldPos = ctx.Auditorium.transform.position; Quaternion oldRot = ctx.Auditorium.transform.rotation;
            WRoom a = l.Hub(RoomKind.Auditorium);
            Quaternion newRot = Quaternion.Euler(0f, a.Yaw, 0f);
            foreach (string follower in new[] { "MainStage", "AuditoriumVoiceZoneTrigger", "Door_Double_Red", "Door_Double_Red (1)" })
            {
                var f = ctx.Find(follower);
                if (f != null) MoveWith(f.transform, oldPos, oldRot, a.Pivot, newRot);
            }
            ctx.Auditorium.transform.SetPositionAndRotation(a.Pivot, newRot);

            // Rear entrances on the upper floor: the decorative leaves become real doors.
            Transform doors = ctx.Auditorium.transform.Find("Doors");
            if (doors != null)
            {
                var rearLeaves = new List<Transform>();
                foreach (Transform leaf in doors)
                {
                    if (leaf.name.EndsWith("(10)") || leaf.name.EndsWith("(11)") || leaf.name.EndsWith("(12)") || leaf.name.EndsWith("(13)"))
                    {
                        leaf.gameObject.SetActive(false);
                        rearLeaves.Add(leaf);
                    }
                }
                // Each door where its pair of leaves hung: the wall openings are there, on the
                // auditorium's back floor (4.0), not centred at +-6.95 on the upper floor (4.5).
                foreach (var pair in rearLeaves.GroupBy(l => Mathf.Sign(l.position.x - a.Pivot.x)))
                {
                    Vector3 at = pair.Aggregate(Vector3.zero, (sum, l) => sum + l.position) / pair.Count();
                    var d = Place("Assets/Prefabs/Rooms/Props/Doors/Door_Double_Red.prefab", hub, at, Quaternion.identity);
                    if (d != null) ctx.ExtraSabotage.Add(d);
                }
            }

            // Backstage door behind the stage when the drawing connects the dressing room straight to it.
            if (a.Room.Doors.Any(dd => dd.Side == Side.Bottom)) Backstage(ctx, a, hub);

            // Bilheteria (upper floor): built by the corridors as part of their outline and furnished by
            // PrototypeFoyer. The Box_Office_Room prefab is still a work in progress (its walls do not meet
            // the corridors, it carries a second Monster), so it is not used.

            // Escape room (upper floor): the existing instance, with its managers, moves.
            WRoom e = l.Hub(RoomKind.Escape);
            ctx.Escape = ctx.Find("Escape_Room");
            if (ctx.Escape != null)
            {
                ctx.Escape.transform.SetPositionAndRotation(e.Pivot, Quaternion.Euler(0f, e.Yaw, 0f));
                // Its Decoration group dresses the old map's corridor up to 39 m away: keep only what is in the room.
                Transform deco = ctx.Escape.transform.Find("Decoration");
                if (deco != null)
                {
                    var core = new Bounds(new Vector3(3.98f, 1.5f, 1.0f), new Vector3(17.8f, 4f, 12.0f));
                    int off = 0;
                    foreach (Transform t in deco.GetComponentsInChildren<Transform>(true).Where(t => t != deco).ToList())
                    {
                        var r = t.GetComponent<Renderer>();
                        if (r == null) continue;
                        Vector3 local = ctx.Escape.transform.InverseTransformPoint(r.bounds.center);
                        if (!core.Contains(local)) { t.gameObject.SetActive(false); off++; }
                    }
                    ctx.Report.AppendLine($"  escape: {off} peças de decoração fora da sala desligadas");
                }
            }

            // Dressing room: the players spawn in it, so the spawn manager moves along.
            WRoom dr = l.Rooms.First(r => r.Room.Kind == RoomKind.Dressing);
            ctx.Dressing = ctx.Find("DressingRoom _Game");
            if (ctx.Dressing != null)
            {
                Vector3 op = ctx.Dressing.transform.position; Quaternion or = ctx.Dressing.transform.rotation;
                Quaternion nr = Quaternion.Euler(0f, dr.Yaw, 0f);
                var spawn = ctx.Find("SpawnManager");
                if (spawn != null) MoveWith(spawn.transform, op, or, dr.Pivot, nr);
                ctx.Dressing.transform.SetPositionAndRotation(dr.Pivot, nr);
            }

            foreach (WRoom w in l.Rooms.Where(r => r.Room.Kind == RoomKind.Bathroom))
                ctx.Bathrooms.Add(Place("Assets/Prefabs/Rooms/Fixed/Bathroom.prefab", hub, w.Pivot, Quaternion.Euler(0f, w.Yaw, 0f)));
        }

        /// <summary>
        /// A door through the stage's back wall, without opening the wall: the two middle panels
        /// (5 x 12 m each) are swapped for copies of themselves cut around a door-sized hole. The
        /// steps down to the corridor are built by the corridors (a stair with a landing).
        /// </summary>
        private static void Backstage(Context ctx, WRoom a, Transform hub)
        {
            Vector3 doorAt = a.DoorPoints.First(kv => kv.Key.Side == Side.Bottom).Value;
            var panels = ctx.Auditorium.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.name.StartsWith("SM_Stage_Wall_V3") && r.bounds.max.z < a.Pivot.z - 14f && r.bounds.min.x < doorAt.x + 4.9f && r.bounds.max.x > doorAt.x - 4.9f).ToList();
            if (panels.Count == 0) { ctx.Report.AppendLine("  PROBLEMA: painéis do fundo do palco não encontrados"); return; }
            Bounds hole = panels[0].bounds;
            foreach (var r in panels) hole.Encapsulate(r.bounds);
            MeshRenderer source = panels[0];
            foreach (var r in panels) r.gameObject.SetActive(false);

            const float half = 0.95f, doorTop = 2.1f;
            float floor = PrototypeCorridors.StageFloor, zc = hole.center.z;
            var fill = Group("FundoDoPalco_Porta", hub).transform;
            void Piece(string name, float x0, float x1, float y0, float y1)
            {
                var target = new Bounds(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, zc), new Vector3(x1 - x0, y1 - y0, hole.size.z));
                var go = Object.Instantiate(source.gameObject, fill);
                go.name = name;
                go.SetActive(true);
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                go.transform.localScale = source.transform.lossyScale;
                Bounds b0 = go.GetComponent<Renderer>().bounds;
                // Stretch each local axis by the ratio of the world axis it lines up with.
                Vector3 s = go.transform.localScale, ratio = new Vector3(target.size.x / b0.size.x, target.size.y / b0.size.y, target.size.z / b0.size.z);
                Matrix4x4 m = Matrix4x4.Rotate(go.transform.rotation);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 axis = m.GetColumn(i);
                    int k = Mathf.Abs(axis.x) > Mathf.Abs(axis.y) && Mathf.Abs(axis.x) > Mathf.Abs(axis.z) ? 0 : Mathf.Abs(axis.y) > Mathf.Abs(axis.z) ? 1 : 2;
                    s[i] *= ratio[k];
                }
                go.transform.localScale = s;
                go.transform.position += target.center - go.GetComponent<Renderer>().bounds.center;
                SetLayerRecursive(go, Walls);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                BoxColliderAt("Col_" + name, fill, target.center, target.size, Walls);
            }
            Piece("Painel_Esquerda", hole.min.x, doorAt.x - half, hole.min.y, hole.max.y);
            Piece("Painel_Direita", doorAt.x + half, hole.max.x, hole.min.y, hole.max.y);
            Piece("Painel_SobreAPorta", doorAt.x - half, doorAt.x + half, floor + doorTop, hole.max.y);
            MarkStatic(fill.gameObject);

            var door = Place("Assets/Prefabs/Rooms/Props/Doors/Door_Double_Black.prefab", hub, new Vector3(doorAt.x, floor, zc), Quaternion.identity);
            if (door != null) ctx.ExtraSabotage.Add(door);
        }

        // ------------------------------------------------------------------ rooms

        private static void PlaceRooms(Context ctx)
        {
            var points = ctx.Find("SpawnRoomPoints");
            if (points == null) points = new GameObject("SpawnRoomPoints");
            for (int i = points.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(points.transform.GetChild(i).gameObject);
            points.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int k = 1;
            foreach (WRoom w in ctx.Layout.Rooms.Where(r => r.Room.Kind == RoomKind.Puzzle).OrderByDescending(r => r.Pivot.z).ThenBy(r => r.Pivot.x))
            {
                var t = new GameObject($"Sala_{k:00}{(w.Room.Upper ? "_cima" : "")}").transform;
                t.SetParent(points.transform, false);
                t.SetPositionAndRotation(w.Pivot, Quaternion.Euler(0f, w.Yaw, 0f));
                ctx.RoomSlots.Add(t);
                k++;
            }

            var spawnRooms = ctx.Find("SpawnRoomManager")?.GetComponent<Rooms.SpawnRooms>();
            if (spawnRooms != null)
            {
                var so = new SerializedObject(spawnRooms);
                var sp = so.FindProperty("spawnPoints");
                sp.arraySize = ctx.RoomSlots.Count;
                for (int i = 0; i < ctx.RoomSlots.Count; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = ctx.RoomSlots[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            ctx.Report.AppendLine($"  vagas de sala sorteada: {ctx.RoomSlots.Count} ({ctx.RoomSlots.Count(t => t.name.EndsWith("_cima"))} no 2º andar)");
        }

        // ------------------------------------------------------------------ match

        [MenuItem("Tools/TCC/Protótipos/Preparar o nível aberto para a partida (portas e NavMesh)", priority = 42)]
        private static void MatchReadyOpen()
        {
            Scene s = SceneManager.GetActiveScene();
            if (!s.path.StartsWith(SceneFolder)) { Debug.LogWarning("Abra uma cena de protótipo primeiro."); return; }
            Debug.Log(MatchReady(s));
        }

        /// <summary>
        /// What a match needs from a finished level, and what hand edits tend to undo.
        ///
        /// <para>Every door with no NetworkObject above it gets its own (same rule as Tools/TCC/Portas):
        /// a Door only works spawned, and the corridor, hub and backstage doors sit in no networked room.</para>
        ///
        /// <para>Nothing the NavMesh collects stays static batched. SpawnRooms rebuilds the NavMesh in the
        /// match, and a batched renderer hands it the scene's "Combined Mesh", which cannot be read: the
        /// editor logs an error for each one and the player build leaves those floors and walls out.</para>
        /// </summary>
        public static string MatchReady(Scene scene)
        {
            var nav = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).FirstOrDefault(n => n != null);
            int doors = 0, unbatched = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Objects.Door door in root.GetComponentsInChildren<Objects.Door>(true))
                {
                    if (door.GetComponentInParent<Unity.Netcode.NetworkObject>(true) != null) continue;
                    door.gameObject.AddComponent<Unity.Netcode.NetworkObject>();
                    doors++;
                }

                if (nav == null) continue;
                foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if ((nav.layerMask & (1 << mr.gameObject.layer)) == 0) continue;
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(mr.gameObject);
                    if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
                    GameObjectUtility.SetStaticEditorFlags(mr.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                    unbatched++;
                }
            }

            if (doors + unbatched > 0) EditorSceneManager.MarkSceneDirty(scene);
            return $"  {scene.name}: {doors} portas ganharam NetworkObject, {unbatched} renderers do NavMesh saíram do static batching";
        }

        // ------------------------------------------------------------------ hub colliders & layers

        private static readonly string[] GroundWords = { "floor", "stage", "step", "stair", "ground", "piso", "chao", "platform" };
        private static readonly string[] WallWords = { "wall", "arch", "column", "collumn", "pillar", "corner", "parede", "railing", "rail", "balcony" };
        private static readonly string[] SkipWords = { "roof", "ceiling", "top", "truss", "light", "chandel", "lamp", "door", "curtain", "cortina", "sign", "poster", "frame", "speaker" };

        private static void FixHubColliders(Context ctx)
        {
            int added = 0;
            foreach (GameObject room in new[] { ctx.Auditorium, ctx.Bilheteria, ctx.Escape, ctx.Dressing }.Concat(ctx.Bathrooms))
            {
                if (room == null) continue;
                foreach (MeshRenderer mr in room.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = mr.name.ToLowerInvariant();
                    if (mr.GetComponentInParent<Unity.Netcode.NetworkObject>(true) != null && mr.GetComponentInParent<Objects.Door>(true) != null) continue;
                    // A wall with a doorway in it (SM_Wall_Bathroom_Single_Door) is still a wall.
                    bool doorwayWall = n.Contains("wall") && n.Contains("door");
                    if (!doorwayWall && SkipWords.Any(n.Contains)) continue;
                    bool ground = GroundWords.Any(n.Contains), wall = WallWords.Any(n.Contains);
                    if (!ground && !wall) continue;
                    mr.gameObject.layer = ground ? Ground : Walls;
                    if (mr.GetComponent<Collider>() != null) continue;
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    mr.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    added++;
                }
            }

            // Auditorium seats: one box per row, on Scenary, so they block like furniture.
            if (ctx.Auditorium != null)
            {
                Transform chairs = ctx.Auditorium.transform.Find("Spline_Chairs");
                if (chairs != null)
                {
                    foreach (Transform row in chairs)
                    {
                        var rs = row.GetComponentsInChildren<Renderer>(true);
                        if (rs.Length == 0 || row.GetComponent<Collider>() != null) continue;
                        Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                        var col = BoxColliderAt("Col_Fileira", row, bb.center, new Vector3(bb.size.x, Mathf.Min(bb.size.y, 1.0f), bb.size.z), Scenary);
                        col.transform.position = new Vector3(bb.center.x, bb.min.y + Mathf.Min(bb.size.y, 1.0f) * 0.5f, bb.center.z);
                        foreach (var r in rs) r.gameObject.layer = Scenary;
                        added++;
                    }
                }
            }
            ctx.Report.AppendLine($"  colisores adicionados nas salas do centro: {added}");
        }

        // ------------------------------------------------------------------ systems

        private static void PlaceSystems(Context ctx, ContractsSO contract)
        {
            PrototypeLayout l = ctx.Layout;
            PrototypeCorridors c = ctx.Corridors;
            WRoom dressing = l.Rooms.First(r => r.Room.Kind == RoomKind.Dressing);
            Vector3 spawn = dressing.Pivot;

            // Candidate points on corridor floors: the lights sit every ~7 m along them.
            var floorPoints = c.LightPoints.Select(p => p + Vector3.up * 0.3f).ToList();
            var far = floorPoints.OrderByDescending(p => Vector3.Distance(p, spawn)).ToList();

            // Patrol sectors made for this map (rooms, hub, corridors), wired into the monster.
            ctx.Report.AppendLine(PrototypeSectors.Build(ctx));

            // Monster starts far from the players.
            var monster = ctx.Find("Monster");
            if (monster != null && far.Count > 0) monster.transform.position = new Vector3(far[0].x, far[0].y - 0.3f, far[0].z);

            // Lost items: spread over the corridors, away from the spawn.
            var lost = ctx.Find("LostItemsSpawns");
            if (lost != null)
            {
                var pts = Spread(far.Take(Mathf.Max(lost.transform.childCount * 2, 1)).ToList(), lost.transform.childCount, new List<Vector3> { spawn });
                int i = 0;
                foreach (Transform t in lost.transform) { if (i < pts.Count) t.position = pts[i] + Vector3.up * 0.0f; i++; }
            }

            // Electric circuit spots: on both sides of the stage, at wall height (where Game1 had them moved to by hand).
            var circuits = ctx.Find("CircuitSpawnPoints");
            if (circuits != null && circuits.transform.childCount >= 2)
            {
                circuits.transform.GetChild(0).position = new Vector3(-9.8f, 2.02f, -14.55f);
                circuits.transform.GetChild(1).position = new Vector3(9.87f, 2.2f, -14.6f);
            }

            // Everything the monster can sabotage: every light and door in the scene.
            var brain = monster != null ? monster.GetComponent("MonsterSabotage") : null;
            if (brain != null)
            {
                var all = new List<GameObject>();
                foreach (GameObject root in ctx.Scene.GetRootGameObjects())
                {
                    foreach (var ls in root.GetComponentsInChildren<Component>(true).Where(x => x != null && (x.GetType().Name == "LightSabotage" || x.GetType().Name == "DoorSabotage")))
                        if (!all.Contains(ls.gameObject)) all.Add(ls.gameObject);
                }
                var so = new SerializedObject(brain);
                var sp = so.FindProperty("allSabotageObjects");
                sp.arraySize = all.Count;
                for (int i = 0; i < all.Count; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                ctx.Report.AppendLine($"  objetos sabotáveis: {all.Count}");
            }

            // The prototype contract: the Hamlet missions in the 14.7 m rooms, decorated rooms in the other slots.
            var missionManager = ctx.Find("MissionManager")?.GetComponent("MissionManager");
            if (missionManager != null)
            {
                var so = new SerializedObject(missionManager);
                so.FindProperty("currentContract").objectReferenceValue = contract;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Whatever the walls leave open shows black, never the editor's blue.
            var mainCam = ctx.Find("Main Camera")?.GetComponent<Camera>();
            if (mainCam != null) { mainCam.clearFlags = CameraClearFlags.SolidColor; mainCam.backgroundColor = Color.black; }

            // The old map's navmesh no longer fits: it is rebuilt (in the editor by the lighting step, and at runtime by SpawnRooms).
            var nav = ctx.Find("NavMesh")?.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
            if (nav != null) nav.navMeshData = null;
        }

        /// <summary>Picks count points far apart from each other and from the ones given.</summary>
        private static List<Vector3> Spread(List<Vector3> candidates, int count, List<Vector3> avoid)
        {
            var chosen = new List<Vector3>();
            var taken = new List<Vector3>(avoid);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                Vector3 best = candidates.OrderByDescending(p => taken.Count == 0 ? 0f : taken.Min(q => Vector3.Distance(p, q))).First();
                chosen.Add(best);
                taken.Add(best);
            }
            return chosen;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
