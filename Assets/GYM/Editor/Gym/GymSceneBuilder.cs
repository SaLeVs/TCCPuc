using System.Collections.Generic;
using System.IO;
using System.Linq;
using LevelDesign.Toolkit;
using Monster;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LevelDesign.Gym.Building
{
    /// <summary>
    /// Generates Assets/LevelDesign/Scenes/GYM.unity: a hall that doubles as a measured running
    /// track, with nine stations off it (loops, cover and vision, heights, crouch and widths, doors,
    /// items, hearing, the monster's lair and a scale showcase). Every number on a label comes from
    /// the Player and Monster prefabs, so regenerating after a balance change updates the GYM.
    /// </summary>
    public static class GymSceneBuilder
    {
        public const string ScenePath = "Assets/LevelDesign/Scenes/GYM.unity";
        private const string NavMeshDataPath = "Assets/LevelDesign/Scenes/GYM/NavMesh-GYM.asset";
        private const string Prefabs = "Assets/Prefabs/";

        // Hall: x[-60,60] z[-5,5]. North stations z[5,33], south stations z[-29,-5], 24 m wide each.
        private const float HallHalfWidth = 5f;
        private const float HallHalfLength = 60f;
        private const float NorthEdge = 33f;
        private const float SouthEdge = -29f;
        private const float DoorwayWidth = 4f;
        private static readonly float[] StationCenters = { -48f, -24f, 0f, 24f, 48f };

        private static GymBuildKit _kit;
        private static GameMetrics _metrics;
        private static readonly List<Transform> Sectors = new();

        [MenuItem("Tools/TCC/Level Design/Gerar cena GYM", priority = 30)]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("GYM", "Saia do Play Mode antes de gerar o GYM.", "Ok");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(Path.Combine(LevelToolkitScanner.ProjectRoot, ScenePath)) &&
                !EditorUtility.DisplayDialog("GYM", "A cena GYM já existe e será gerada de novo do zero (mudanças feitas à mão nela se perdem). Continuar?", "Gerar de novo", "Cancelar"))
            {
                return;
            }

            Build();
        }

        /// <summary>No dialogs: for automation. Refuses if the open scene has unsaved changes.</summary>
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Saia do Play Mode antes.";

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.isDirty && open.path != ScenePath) return $"A cena {open.name} tem mudanças não salvas.";
            }

            _metrics = GameMetrics.Read();
            var catalog = AssetDatabase.LoadAssetAtPath<LevelToolkitCatalog>(LevelToolkitScanner.CatalogPath) ?? LevelToolkitScanner.Scan();
            _kit = new GymBuildKit(catalog);
            Sectors.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            var world = _kit.Group("GYM").transform;
            var labels = _kit.Group("GYM_Textos").transform;

            BuildShell(world, labels);
            var spawn = BuildHall(world, labels);
            BuildLoopShort(world, labels);
            BuildCoverAndVision(world, labels);
            BuildHeights(world, labels);
            BuildCrouchAndWidths(world, labels);
            BuildLoopWithDoors(world, labels);
            BuildDoors(world, labels);
            BuildItems(world, labels);
            var hearingSpot = BuildHearing(world, labels);
            var home = BuildLair(world, labels);
            BuildShowcase(world, labels);

            var monster = BuildMonster(home);
            BuildSystems(spawn, monster, home);
            WireHearingPads(world, hearingSpot);

            BuildNavMesh();

            LevelToolkitScanner.EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string message = $"GYM gerado em {ScenePath}. Dê Play nesta cena.";
            Debug.Log(message);
            return message;
        }

        // ---------------------------------------------------------------- shell

        private static void BuildLighting()
        {
            var sun = new GameObject("Luz direcional").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.43f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.24f);
        }

        private static void BuildShell(Transform world, Transform labels)
        {
            var shell = _kit.Group("Estrutura", world).transform;

            float width = HallHalfLength * 2f;
            float depth = NorthEdge - SouthEdge;
            _kit.Box(shell, "Chão", new Vector3(0f, -0.2f, (NorthEdge + SouthEdge) * 0.5f), new Vector3(width + 1f, 0.2f, depth + 1f), _kit.Floor, _kit.LayerGround);

            // Outer walls.
            _kit.Wall(shell, new Vector3(-HallHalfLength, 0, SouthEdge), new Vector3(HallHalfLength, 0, SouthEdge));
            _kit.Wall(shell, new Vector3(-HallHalfLength, 0, NorthEdge), new Vector3(HallHalfLength, 0, NorthEdge));
            _kit.Wall(shell, new Vector3(-HallHalfLength, 0, SouthEdge), new Vector3(-HallHalfLength, 0, NorthEdge));
            _kit.Wall(shell, new Vector3(HallHalfLength, 0, SouthEdge), new Vector3(HallHalfLength, 0, NorthEdge));

            // Hall walls with one doorway per station, and the dividers between stations.
            _kit.WallAlongX(shell, HallHalfWidth, -HallHalfLength, HallHalfLength, DoorwayWidth, StationCenters);
            _kit.WallAlongX(shell, -HallHalfWidth, -HallHalfLength, HallHalfLength, DoorwayWidth, StationCenters);

            foreach (float x in new[] { -36f, -12f, 12f, 36f })
            {
                _kit.Wall(shell, new Vector3(x, 0, HallHalfWidth), new Vector3(x, 0, NorthEdge));
                _kit.Wall(shell, new Vector3(x, 0, -HallHalfWidth), new Vector3(x, 0, SouthEdge));
            }
        }

        private static void StationTitle(Transform labels, float x, bool north, string title, string subtitle)
        {
            float z = north ? HallHalfWidth - 0.17f : -HallHalfWidth + 0.17f;
            var rotation = GymBuildKit.FacingViewer(north ? Vector3.back : Vector3.forward);

            // Above the doorway, on the hall side, readable as you walk the hall.
            _kit.Label(labels, $"<b>{title}</b>\n<size=60%>{subtitle}</size>", new Vector3(x, 2.45f, z), rotation, 3.2f, 7.5f, new Color(1f, 0.85f, 0.35f));
        }

        private static void Sector(Transform parent, string name, Vector3 position, float radius)
        {
            var sectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Managers/MapStages/SectorStage.prefab");
            var go = sectorPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(sectorPrefab, parent) : new GameObject();
            go.name = name;
            go.transform.localPosition = position;

            var sector = go.GetComponent<PatrolSector>() ?? go.AddComponent<PatrolSector>();
            var so = new SerializedObject(sector);
            so.FindProperty("patrolSectorRadius").floatValue = radius;
            so.FindProperty("patrolRadiusColor").colorValue = new Color(1f, 0.3f, 0.2f, 0.12f);
            so.ApplyModifiedPropertiesWithoutUndo();

            Sectors.Add(go.transform);
        }

        // ---------------------------------------------------------------- hall / running track

        private static Transform BuildHall(Transform world, Transform labels)
        {
            var hall = _kit.Group("Corredor central (pista)", world).transform;
            var east = Vector3.right;

            var spawn = _kit.Marker(hall, "Spawn", new Vector3(-1.5f, 0.05f, -2f), east);

            // Timing lines: start a few steps ahead of the spawn (so spawning does not start the
            // clock), a split every 10 m, finish near the east wall.
            const float startX = 2f;
            _kit.Pad(hall, "LARGADA", new Vector3(startX, 0f, 0f), new Vector2(0.8f, HallHalfWidth * 2f - 0.4f), GymPadAction.TimerStart, null, _kit.PadBlue, east);
            for (int metres = 10; metres <= 50; metres += 10)
            {
                _kit.Pad(hall, $"{metres} m", new Vector3(startX + metres, 0f, 0f), new Vector2(0.5f, HallHalfWidth * 2f - 0.4f), GymPadAction.TimerSplit, null, _kit.PadBlue, east);
                _kit.Label(labels, $"{metres} m", new Vector3(startX + metres + 1.6f, 0.02f, 2.8f), GymBuildKit.OnFloor(east), 9f, 6f, new Color(1f, 0.85f, 0.3f, 0.9f));
            }

            float finish = HallHalfLength - 3f;
            _kit.Pad(hall, $"{finish - startX:0} m · CHEGADA", new Vector3(finish, 0f, 0f), new Vector2(0.8f, HallHalfWidth * 2f - 0.4f), GymPadAction.TimerStop, null, _kit.PadBlue, east);

            // Distances that matter, drawn on the floor from the start line.
            float sprint = _metrics.runSpeed * _metrics.SprintSeconds;
            Special(hall, labels, startX + sprint, $"1 sprint inteiro\n{sprint:0.#} m em {_metrics.SprintSeconds:0.#} s");
            Special(hall, labels, startX + _metrics.walkSpeed * 10f, $"10 s andando\n{_metrics.walkSpeed * 10f:0.#} m");
            Special(hall, labels, startX + _metrics.visionDistance, $"alcance da visão\ndo monstro: {_metrics.visionDistance:0} m");

            // Chase pads west of the spawn, off to the north side so you can walk past them.
            foreach (float distance in new[] { 10f, 20f, 30f })
            {
                float padX = -4f - (distance - 10f) * 0.4f;
                var target = _kit.Marker(hall, $"Monstro a {distance:0} m", new Vector3(padX - distance, 0f, 2.5f), east);
                _kit.Pad(hall, $"MONSTRO\n{distance:0} m", new Vector3(padX, 0f, 3f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterChaseFrom, target, _kit.PadRed, Vector3.left);
            }

            _kit.Label(labels, "Pise num quadrado vermelho:\no monstro aparece a essa distância.\nDepois corra para o leste (pista).", new Vector3(-8f, 0.02f, -1.5f), GymBuildKit.OnFloor(Vector3.left), 2.6f, 9f);

            // Boards on the hall walls by the spawn.
            _kit.Label(labels, "<b>MÉTRICAS DO JOGO</b>\n<size=70%>" + _metrics.ToReadableText() + "</size>",
                new Vector3(-12f, 2.85f, -HallHalfWidth + 0.17f), GymBuildKit.FacingViewer(Vector3.forward), 1.6f, 16f, Color.white, TMPro.TextAlignmentOptions.TopLeft);

            _kit.Label(labels, "<b>COMO USAR O GYM</b>\n<size=75%>" +
                               "• O monstro começa como estátua no covil (S4). F3 solta / pausa.\n" +
                               "• Quadrados vermelhos: monstro aparece e persegue.\n" +
                               "• Linhas azuis: cronômetro da pista (veja o painel, F1).\n" +
                               "• F2 monstro atrás de você · F4 cura · F5 spawn · F6 monstro p/ casa\n" +
                               "• F7 stamina infinita · F8 invencível · F9 cone de visão · F10 reinicia\n" +
                               "• Chão e paredes têm grade: linha fina = 1 m, grossa = 5 m.</size>",
                new Vector3(12f, 2.85f, HallHalfWidth - 0.17f), GymBuildKit.FacingViewer(Vector3.back), 1.6f, 16f, Color.white, TMPro.TextAlignmentOptions.TopLeft);

            Sector(hall, "Setor corredor oeste", new Vector3(-36f, 0f, 0f), 14f);
            Sector(hall, "Setor corredor centro", new Vector3(0f, 0f, 0f), 14f);
            Sector(hall, "Setor corredor leste", new Vector3(36f, 0f, 0f), 14f);

            return spawn;
        }

        private static void Special(Transform hall, Transform labels, float x, string text)
        {
            if (x >= HallHalfLength - 1f) return;
            _kit.FloorStripe(hall, new Vector3(x, 0f, 0f), Vector3.forward, HallHalfWidth * 2f - 0.6f, 0.12f, _kit.StripeSpecial);
            _kit.Label(labels, text, new Vector3(x - 1.4f, 0.02f, -2.6f), GymBuildKit.OnFloor(Vector3.right), 2.6f, 4.6f, new Color(0.5f, 1f, 1f));
        }

        // ---------------------------------------------------------------- N1: short loop

        private static void BuildLoopShort(Transform world, Transform labels)
        {
            var root = _kit.Group("N1 · Loop curto", world).transform;
            StationTitle(labels, -48f, true, "N1 · LOOP CURTO", "dá para girar o monstro em volta do bloco?");

            _kit.Box(root, "Bloco 8×8", new Vector3(-48f, 0f, 19f), new Vector3(8f, GymBuildKit.WallHeight, 8f), _kit.WallMat, _kit.LayerWalls);
            _kit.Box(root, "Pilar 2×2", new Vector3(-40f, 0f, 28.5f), new Vector3(2f, GymBuildKit.WallHeight, 2f), _kit.WallMat, _kit.LayerWalls);
            _kit.Box(root, "Pilar 1×1", new Vector3(-56f, 0f, 28.5f), new Vector3(1f, GymBuildKit.WallHeight, 1f), _kit.WallMat, _kit.LayerWalls);

            float perimeter = (8f + 3f) * 4f;
            _kit.Label(labels, $"Bloco 8×8 m · volta a 1,5 m da parede: ~{perimeter:0} m\nandando: {perimeter / _metrics.walkSpeed:0.#} s · monstro: {perimeter / _metrics.monsterChaseSpeed:0.#} s",
                new Vector3(-48f, 0.02f, 13.2f), GymBuildKit.OnFloor(Vector3.forward), 2.4f, 10f);
            _kit.Label(labels, "pilar 2×2", new Vector3(-40f, 0.02f, 26.6f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 4f);
            _kit.Label(labels, "pilar 1×1", new Vector3(-56f, 0.02f, 26.6f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 4f);

            var target = _kit.Marker(root, "Monstro atrás do bloco", new Vector3(-48f, 0f, 29.5f), Vector3.back);
            _kit.Pad(root, "MONSTRO\natrás do bloco", new Vector3(-52f, 0f, 8.5f), new Vector2(2.6f, 2.6f), GymPadAction.MonsterChaseFrom, target, _kit.PadRed, Vector3.forward);
            _kit.Pad(root, "CURAR", new Vector3(-44f, 0f, 8.5f), new Vector2(2f, 2f), GymPadAction.Heal, null, _kit.PadGreen, Vector3.forward);

            Sector(root, "Setor N1", new Vector3(-48f, 0f, 19f), 11f);
        }

        // ---------------------------------------------------------------- N2: cover & vision

        private static void BuildCoverAndVision(Transform world, Transform labels)
        {
            var root = _kit.Group("N2 · Cobertura e visão", world).transform;
            StationTitle(labels, -24f, true, "N2 · COBERTURA E VISÃO", "o monstro vira estátua: ande e veja se ele te enxerga (F9)");

            var statue = _kit.Marker(root, "Estátua", new Vector3(-24f, 0f, 30.5f), Vector3.back);
            _kit.Pad(root, "MONSTRO\nESTÁTUA", new Vector3(-28f, 0f, 7.5f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterStatueAt, statue, _kit.PadYellow, Vector3.forward);
            _kit.Pad(root, "SOLTAR\nMONSTRO", new Vector3(-20f, 0f, 7.5f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterResume, null, _kit.PadGreen, Vector3.forward);

            // Row A: low walls of growing height — which ones hide you crouched, which standing?
            float[] heights = { 1.0f, 1.2f, 1.5f, 2.0f };
            float[] xs = { -32.5f, -28.5f, -19.5f, -15.5f };
            for (int i = 0; i < heights.Length; i++)
            {
                _kit.Box(root, $"Mureta {heights[i]:0.0} m", new Vector3(xs[i], 0f, 22f), new Vector3(2.4f, heights[i], 0.4f), _kit.Cover, _kit.LayerWalls);
                string hides = heights[i] >= _metrics.standHeight ? "esconde em pé" : heights[i] >= _metrics.crouchHeight ? "esconde agachado" : "não esconde";
                _kit.Label(labels, $"{heights[i]:0.0} m\n<size=70%>{hides}</size>", new Vector3(xs[i], 0.02f, 20.6f), GymBuildKit.OnFloor(Vector3.forward), 2.4f, 3.4f);
            }

            // Row B: real props from the toolkit, with their codes.
            (string path, float x)[] props =
            {
                ("Rooms/Props/Other_Rooms/SM_Iron_Locker.prefab", -32.5f),
                ("Rooms/Props/Other_Rooms/SM_Metal_Shelf_V1.prefab", -28.5f),
                ("Rooms/Props/Dressing_Room/SM_Biombo.prefab", -24f),
                ("Rooms/Props/Dressing_Room/ConjuntoFlightCases.prefab", -19.5f),
                ("Rooms/Props/Dressing_Room/SM_Costume_Box.prefab", -15.5f),
            };

            foreach (var (path, x) in props)
            {
                _kit.PlacePrefab(root, Prefabs + path, new Vector3(x, 0f, 16f));
                _kit.Label(labels, $"{_kit.CodeOf(Prefabs + path)}\n<size=70%>{GymBuildKit.NameOf(path)}</size>", new Vector3(x, 0.02f, 14.4f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 4f);
            }

            // Row C: the layers that do NOT block the monster's eyes.
            _kit.Box(root, "Caixa layer Props", new Vector3(-31f, 0f, 11f), new Vector3(2f, 2f, 0.6f), _kit.Warning, _kit.LayerProps);
            _kit.Label(labels, "layer <b>Props</b>\n<size=70%>colide, mas não bloqueia a visão</size>", new Vector3(-31f, 0.02f, 9.8f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 5f);

            _kit.Box(root, "Caixa layer Walls", new Vector3(-26f, 0f, 11f), new Vector3(2f, 2f, 0.6f), _kit.Cover, _kit.LayerWalls);
            _kit.Label(labels, "layer <b>Walls</b>\n<size=70%>bloqueia a visão</size>", new Vector3(-26f, 0.02f, 9.8f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 5f);

            var door = _kit.PlacePrefab(root, Prefabs + "Rooms/Props/Doors/Door_Black.prefab", new Vector3(-19f, 0f, 11f));
            EnsureNetworked(door);
            _kit.Label(labels, "porta solta\n<size=70%>fechada, ela tapa a visão?</size>", new Vector3(-19f, 0.02f, 9.8f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 5f);

            Sector(root, "Setor N2", new Vector3(-24f, 0f, 19f), 11f);
        }

        // ---------------------------------------------------------------- N3: heights

        private static void BuildHeights(Transform world, Transform labels)
        {
            var root = _kit.Group("N3 · Alturas", world).transform;
            float slope = NavMesh.GetSettingsByID(0).agentSlope;
            float climb = NavMesh.GetSettingsByID(0).agentClimb;
            StationTitle(labels, 0f, true, "N3 · ALTURAS", $"degrau máx. do jogador {_metrics.maxStepHeight:0.##} m · monstro sobe rampa até {slope:0}°");

            // Single steps.
            float[] steps = { 0.1f, 0.2f, 0.3f, 0.35f, 0.4f, 0.5f };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = -9f + i * 3.4f;
                _kit.Box(root, $"Degrau {steps[i]:0.00} m", new Vector3(x, 0f, 10f), new Vector3(2f, steps[i], 2f), _kit.Height, _kit.LayerGround);
                string player = steps[i] <= _metrics.maxStepHeight + 0.001f ? "jogador sobe" : "jogador não sobe";
                string monster = steps[i] <= climb + 0.001f ? "monstro sobe" : "monstro não sobe";
                _kit.Label(labels, $"{steps[i]:0.00} m\n<size=65%>{player}\n{monster}</size>", new Vector3(x, 0.02f, 8.1f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 3.2f);
            }

            // Ramps up to 1 m at growing angles.
            float[] angles = { 10f, 20f, 30f, 40f };
            for (int i = 0; i < angles.Length; i++)
            {
                float x = 1.5f + i * 2.8f;
                float run = _kit.Ramp(root, $"Rampa {angles[i]:0}°", new Vector3(x, 0f, 14f), Vector3.forward, 2.2f, 1f, angles[i]);
                string monster = angles[i] <= slope + 0.01f ? "monstro sobe" : "monstro não sobe";
                _kit.Label(labels, $"{angles[i]:0}°\n<size=65%>{run:0.0} m de rampa\n{monster}</size>", new Vector3(x, 0.02f, 12.9f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 2.8f);
            }

            // Stairs to a 2 m lookout with an open drop on the far side.
            const float rise = 0.2f, tread = 0.4f;
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                _kit.Box(root, $"Escada {i + 1}", new Vector3(-8f, 0f, 14f + i * tread + tread * 0.5f), new Vector3(3f, rise * (i + 1), tread), _kit.Height, _kit.LayerGround);
            }

            // Starts where the last step ends (z = 14 + 10 × 0.4).
            _kit.Box(root, "Mirante 2 m", new Vector3(-6f, 0f, 20f), new Vector3(7f, 2f, 4f), _kit.Height, _kit.LayerGround);
            _kit.Label(labels, $"escada: degrau {rise:0.0} m × {tread:0.0} m\nmirante a 2 m · <b>queda</b> no lado leste", new Vector3(-8f, 0.02f, 12.9f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 5f);

            // A stage-height platform (1.5 m, like SM_Stage) reached only by the ramps' landings.
            _kit.Box(root, "Palco 1,5 m", new Vector3(0f, 0f, 29f), new Vector3(10f, 1.5f, 6f), _kit.Height, _kit.LayerGround);
            _kit.Label(labels, "palco 1,5 m (altura do SM_Stage)\nsem acesso: dá para subir?", new Vector3(0f, 0.02f, 25.4f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 8f);

            Sector(root, "Setor N3", new Vector3(0f, 0f, 19f), 11f);
        }

        // ---------------------------------------------------------------- N4: crouch & widths

        private static void BuildCrouchAndWidths(Transform world, Transform labels)
        {
            var root = _kit.Group("N4 · Agachar e larguras", world).transform;
            StationTitle(labels, 24f, true, "N4 · AGACHAR E LARGURAS", $"jogador: {_metrics.standHeight:0.##} m em pé · {_metrics.crouchHeight:0.##} m agachado · monstro {_metrics.monsterHeight:0.#} m");

            // Tunnels: full-height walls between them, a filler block above the opening.
            float[] heights = { 1.0f, 1.2f, 1.3f, 1.5f, 1.8f };
            const float tunnelWidth = 2f, z0 = 10f, z1 = 16f;
            float x = 13.5f;
            _kit.Wall(root, new Vector3(x, 0, z0), new Vector3(x, 0, z1));

            foreach (float h in heights)
            {
                float center = x + tunnelWidth * 0.5f + GymBuildKit.WallThickness * 0.5f;
                _kit.Box(root, $"Teto túnel {h:0.0} m", new Vector3(center, h, (z0 + z1) * 0.5f), new Vector3(tunnelWidth, GymBuildKit.WallHeight - h, z1 - z0), _kit.Crouch, _kit.LayerWalls);

                string player = h > _metrics.standHeight ? "passa em pé" : h > _metrics.crouchHeight ? "passa agachado" : "não passa";
                string monster = h >= _metrics.monsterHeight ? "monstro passa" : "monstro não passa";
                _kit.Label(labels, $"{h:0.0} m\n<size=65%>{player}\n{monster}</size>", new Vector3(center, 0.02f, z0 - 1.2f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 2.4f);

                x += tunnelWidth + GymBuildKit.WallThickness;
                _kit.Wall(root, new Vector3(x, 0, z0), new Vector3(x, 0, z1));
            }

            // Corridors of growing width.
            float[] widths = { 0.6f, 0.8f, 1.0f, 1.5f, 2.0f, 3.0f };
            const float c0 = 21f, c1 = 31f;
            x = 13.5f;
            _kit.Wall(root, new Vector3(x, 0, c0), new Vector3(x, 0, c1));

            foreach (float w in widths)
            {
                float center = x + w * 0.5f + GymBuildKit.WallThickness * 0.5f;
                string fit = w < _metrics.playerRadius * 2f ? "jogador não passa" : w < _metrics.monsterRadius * 2f + 0.3f ? "apertado" : "passa";
                _kit.Label(labels, $"{w:0.0} m\n<size=65%>{fit}</size>", new Vector3(center, 0.02f, c0 - 1.1f), GymBuildKit.OnFloor(Vector3.forward), 1.8f, Mathf.Max(w, 1.6f));

                x += w + GymBuildKit.WallThickness;
                _kit.Wall(root, new Vector3(x, 0, c0), new Vector3(x, 0, c1));
            }

            _kit.Label(labels, $"diâmetro: jogador {_metrics.playerRadius * 2f:0.00} m · monstro {_metrics.monsterRadius * 2f:0.00} m",
                new Vector3(19f, 0.02f, 18.6f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 10f);

            Sector(root, "Setor N4", new Vector3(30f, 0f, 19f), 8f);
        }

        // ---------------------------------------------------------------- N5: long loop with doors

        private static void BuildLoopWithDoors(Transform world, Transform labels)
        {
            var root = _kit.Group("N5 · Loop com portas", world).transform;
            StationTitle(labels, 48f, true, "N5 · LOOP COM PORTAS", $"porta segura o monstro ~{_metrics.doorLockSeconds:0.#} s quando ele força");

            // Leaves a 4 m corridor east and west (where the doors go) and 6 m north and south.
            _kit.Box(root, "Bloco 16×16", new Vector3(48f, 0f, 19f), new Vector3(16f, GymBuildKit.WallHeight, 16f), _kit.WallMat, _kit.LayerWalls);

            DoorInCorridor(root, Prefabs + "Rooms/Props/Doors/Door_Double_Black.prefab", 36f, 40f, 19f);
            DoorInCorridor(root, Prefabs + "Rooms/Props/Doors/Door_Black.prefab", 56f, 60f, 19f);

            float perimeter = (16f + 4f) * 4f;
            _kit.Label(labels, $"volta: ~{perimeter:0} m · andando {perimeter / _metrics.walkSpeed:0} s · monstro {perimeter / _metrics.monsterChaseSpeed:0} s\nporta dupla a oeste · porta simples a leste",
                new Vector3(48f, 0.02f, 8f), GymBuildKit.OnFloor(Vector3.forward), 2.2f, 14f);

            var target = _kit.Marker(root, "Monstro no fundo", new Vector3(48f, 0f, 30f), Vector3.back);
            _kit.Pad(root, "MONSTRO\nno fundo", new Vector3(42f, 0f, 7.5f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterChaseFrom, target, _kit.PadRed, Vector3.forward);
            _kit.Pad(root, "CURAR", new Vector3(54f, 0f, 7.5f), new Vector2(2f, 2f), GymPadAction.Heal, null, _kit.PadGreen, Vector3.forward);

            Sector(root, "Setor N5", new Vector3(48f, 0f, 19f), 13f);
        }

        /// <summary>Closes a corridor running along Z (between x0 and x1) at z with a door and wall stubs.</summary>
        private static void DoorInCorridor(Transform root, string doorPath, float x0, float x1, float z)
        {
            var door = _kit.PlacePrefab(root, doorPath, new Vector3((x0 + x1) * 0.5f, 0f, z));
            if (door == null) return;

            EnsureNetworked(door);
            float doorWidth = LevelToolkitScanner.RendererBounds(door).size.x;
            float left = (x0 + x1 - doorWidth) * 0.5f;
            float right = (x0 + x1 + doorWidth) * 0.5f;

            _kit.Wall(root, new Vector3(x0, 0, z), new Vector3(left - 0.05f, 0, z));
            _kit.Wall(root, new Vector3(right + 0.05f, 0, z), new Vector3(x1, 0, z));
            _kit.Wall(root, new Vector3(left, 0, z), new Vector3(right, 0, z), GymBuildKit.WallHeight - 2.15f, 2.15f, null, "Verga");
        }

        // ---------------------------------------------------------------- S1: doors

        private static void BuildDoors(Transform world, Transform labels)
        {
            var root = _kit.Group("S1 · Portas", world).transform;
            StationTitle(labels, -48f, false, "S1 · PORTAS", "todas as portas do toolkit · o monstro está do outro lado");

            string[] doors =
            {
                "Door_Black", "Door_Blue", "Door_Red", "Door_Exit", "Door_Dressing_Room",
                "Door_Double_Black", "Door_Double_Blue", "Door_Double_Red", "Door_Double_Exit",
            };

            const float z = -17f, x0 = -60f, x1 = -36f;
            var placed = new List<(GameObject go, string path, float width)>();

            foreach (string name in doors)
            {
                string path = Prefabs + $"Rooms/Props/Doors/{name}.prefab";
                var door = _kit.PlacePrefab(root, path, new Vector3(0f, 0f, z));
                if (door == null) continue;
                EnsureNetworked(door);
                placed.Add((door, path, LevelToolkitScanner.RendererBounds(door).size.x));
            }

            float gap = (x1 - x0 - placed.Sum(p => p.width)) / (placed.Count + 1);
            float cursor = x0;

            foreach (var (go, path, width) in placed)
            {
                _kit.Wall(root, new Vector3(cursor, 0, z), new Vector3(cursor + gap - 0.05f, 0, z));
                cursor += gap;

                float center = cursor + width * 0.5f;
                var bounds = LevelToolkitScanner.RendererBounds(go);
                go.transform.position += new Vector3(center - bounds.center.x, 0f, 0f);

                _kit.Wall(root, new Vector3(cursor, 0, z), new Vector3(cursor + width, 0, z), GymBuildKit.WallHeight - 2.15f, 2.15f, null, "Verga");
                _kit.Label(labels, $"<b>{_kit.CodeOf(path)}</b>\n<size=70%>{GymBuildKit.NameOf(path)}</size>", new Vector3(center, 2.55f, z + 0.17f), GymBuildKit.FacingViewer(Vector3.forward), 1.5f, Mathf.Max(width + 0.6f, 1.6f));
                cursor += width;
            }

            _kit.Wall(root, new Vector3(cursor + 0.05f, 0, z), new Vector3(x1, 0, z));

            var target = _kit.Marker(root, "Monstro atrás das portas", new Vector3(-48f, 0f, -25f), Vector3.forward);
            _kit.Pad(root, "MONSTRO\natrás das portas", new Vector3(-52f, 0f, -8.5f), new Vector2(2.6f, 2.6f), GymPadAction.MonsterChaseFrom, target, _kit.PadRed, Vector3.back);
            _kit.Pad(root, "CURAR", new Vector3(-44f, 0f, -8.5f), new Vector2(2f, 2f), GymPadAction.Heal, null, _kit.PadGreen, Vector3.back);

            Sector(root, "Setor S1", new Vector3(-48f, 0f, -17f), 11f);
        }

        // ---------------------------------------------------------------- S2: items

        private static void BuildItems(Transform world, Transform labels)
        {
            var root = _kit.Group("S2 · Itens e interativos", world).transform;
            StationTitle(labels, -24f, false, "S2 · ITENS E INTERATIVOS", $"alcance de interação: {_metrics.interactDistance:0.##} m");

            (string path, float x, float z)[] onTables =
            {
                ("Items/Pickables/FlashlightBatteryPickable.prefab", -33f, -11f),
                ("Items/Pickables/FlashlightBatteryPickable.prefab", -32f, -11f),
                ("Items/Pickables/FlashlightBatteryPickable.prefab", -31f, -11f),
                ("Items/Disket.prefab", -28f, -11f),
                ("Items/Pickables/Missions/Box/BoxCirclePickable.prefab", -24f, -11f),
                ("Items/Pickables/Missions/Box/BoxSquarePickable.prefab", -22.5f, -11f),
                ("Items/Pickables/Missions/Box/BoxStarPickable.prefab", -21f, -11f),
                ("Items/Pickables/Missions/Box/BoxTrianglePickable.prefab", -19.5f, -11f),
                ("Audience_Objects/Sad_Mask_Low.prefab", -32f, -19f),
                ("Audience_Objects/SM_Crown.prefab", -29f, -19f),
                ("Audience_Objects/Sword.prefab", -26f, -19f),
            };

            const float tableHeight = 0.8f;
            _kit.Box(root, "Mesa itens", new Vector3(-26f, 0f, -11f), new Vector3(16f, tableHeight, 1.2f), _kit.Table, _kit.LayerScenary);
            _kit.Box(root, "Mesa audiência", new Vector3(-29f, 0f, -19f), new Vector3(9f, tableHeight, 1.2f), _kit.Table, _kit.LayerScenary);

            foreach (var (path, x, z) in onTables)
            {
                FreeFromMission(_kit.PlacePrefab(root, Prefabs + path, new Vector3(x, tableHeight, z)));
            }

            _kit.Label(labels, "pilhas da lanterna · disquete · caixas das missões\n<size=70%>aqui as caixas pegam livres, sem dono de missão · o totem ainda precisa do manager do puzzle</size>",
                new Vector3(-26f, 0.02f, -9.2f), GymBuildKit.OnFloor(Vector3.back), 2.2f, 14f);

            BuildCounter(root, labels);
            _kit.Label(labels, "objetos de audiência: filme-os (dão audiência)", new Vector3(-29f, 0.02f, -21.2f), GymBuildKit.OnFloor(Vector3.back), 2.2f, 9f);

            // Puzzle pieces stand on the floor next to the tables.
            _kit.PlacePrefab(root, Prefabs + "Missions/Totems/Lamps/LampTotem.prefab", new Vector3(-17f, 0f, -20f), 180f);
            _kit.Label(labels, $"{_kit.CodeOf(Prefabs + "Missions/Totems/Lamps/LampTotem.prefab")}\n<size=70%>totem de lâmpada</size>", new Vector3(-17f, 0.02f, -22.6f), GymBuildKit.OnFloor(Vector3.back), 2f, 4f);

            Sector(root, "Setor S2", new Vector3(-24f, 0f, -17f), 10f);
        }

        /// <summary>
        /// The mission boxes only answer the player who owns their puzzle, and in a match the puzzle
        /// that spawns them hands them over. Nothing does here, so the filter turned everyone away.
        /// </summary>
        private static void FreeFromMission(GameObject item)
        {
            var filter = item != null ? item.GetComponent<Missions.MissionOwnershipFilter>() : null;
            if (filter != null) Object.DestroyImmediate(filter);
        }

        /// <summary>
        /// The box-office counter: its flap is a door that lifts. The pad drops the monster behind
        /// it, so it has to come through the passage and force the flap to reach the player.
        /// </summary>
        private static void BuildCounter(Transform root, Transform labels)
        {
            const string path = Prefabs + "Rooms/Props/Box_Office/SM_Cabinet_New.prefab";

            var counter = _kit.PlacePrefab(root, path, new Vector3(-20.5f, 0f, -25.2f));
            if (counter == null) return;

            EnsureNetworked(counter);

            var behind = _kit.Marker(root, "Monstro atrás do balcão", new Vector3(-21f, 0f, -26f), Vector3.right);
            _kit.Pad(root, "MONSTRO\natrás do balcão", new Vector3(-13.8f, 0f, -25.8f), new Vector2(1.8f, 1.8f),
                GymPadAction.MonsterChaseFrom, behind, _kit.PadRed, Vector3.back);

            _kit.Label(labels, $"{_kit.CodeOf(path)}\n<size=70%>balcão · a tampa levanta (E) · o monstro força</size>",
                new Vector3(-22f, 0.02f, -21.9f), GymBuildKit.OnFloor(Vector3.back), 2f, 6f);
        }

        // ---------------------------------------------------------------- S3: hearing

        private static Transform BuildHearing(Transform world, Transform labels)
        {
            var root = _kit.Group("S3 · Audição", world).transform;
            StationTitle(labels, 0f, false, "S3 · AUDIÇÃO", $"monstro de costas · ouve até {_metrics.hearingRange:0} m · ande, corra, agache");

            var spot = _kit.Marker(root, "Estátua de costas", new Vector3(0f, 0f, -27f), Vector3.back);

            foreach (float d in new[] { 5f, 10f, 15f, 20f })
            {
                float z = -27f + d;
                _kit.FloorStripe(root, new Vector3(0f, 0f, z), Vector3.right, 22f, 0.1f, _kit.Stripe);
                _kit.Label(labels, $"{d:0} m", new Vector3(-9f, 0.02f, z + 0.9f), GymBuildKit.OnFloor(Vector3.back), 3.2f, 3f, new Color(1f, 0.85f, 0.3f));
            }

            Sector(root, "Setor S3", new Vector3(0f, 0f, -17f), 10f);
            return spot;
        }

        private static void WireHearingPads(Transform world, Transform spot)
        {
            var root = world.Find("S3 · Audição");
            _kit.Pad(root, "MONSTRO\nde costas", new Vector3(-5f, 0f, -8f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterStatueAt, spot, _kit.PadYellow, Vector3.back);
            _kit.Pad(root, "ZERAR\nawareness", new Vector3(0f, 0f, -8f), new Vector2(2.4f, 2.4f), GymPadAction.ClearAwareness, null, _kit.PadBlue, Vector3.back);
            _kit.Pad(root, "SOLTAR\nMONSTRO", new Vector3(5f, 0f, -8f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterResume, null, _kit.PadGreen, Vector3.back);
        }

        // ---------------------------------------------------------------- S4: lair

        private static Transform BuildLair(Transform world, Transform labels)
        {
            var root = _kit.Group("S4 · Covil do monstro", world).transform;
            StationTitle(labels, 24f, false, "S4 · COVIL DO MONSTRO", "ele começa aqui como estátua · F3 solta");

            var home = _kit.Marker(root, "Casa do monstro", new Vector3(24f, 0f, -22f), Vector3.forward);
            _kit.Pad(root, "SOLTAR\nMONSTRO", new Vector3(20f, 0f, -8f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterResume, null, _kit.PadGreen, Vector3.back);
            _kit.Pad(root, "MONSTRO\nP/ CASA", new Vector3(28f, 0f, -8f), new Vector2(2.4f, 2.4f), GymPadAction.MonsterHome, null, _kit.PadYellow, Vector3.back);

            Sector(root, "Setor S4", new Vector3(24f, 0f, -17f), 10f);
            return home;
        }

        // ---------------------------------------------------------------- S5: scale showcase

        private static void BuildShowcase(Transform world, Transform labels)
        {
            var root = _kit.Group("S5 · Escala das peças reais", world).transform;
            StationTitle(labels, 48f, false, "S5 · ESCALA DAS PEÇAS REAIS", "arte do jogo ao lado da grade · repare quais não têm colisor");

            string[] pieces =
            {
                "Rooms/Props/Hallway/SM_Sophisticated_Wall_4m.prefab",
                "Rooms/Props/Hallway/SM_Sophisticated_Wall_2m.prefab",
                "Rooms/Props/Hallway/SM_Sophisticated_Wall_Single_Door_2m_V1.prefab",
                "Rooms/Props/Dressing_Room/SM_Smooth_Big_Wall.prefab",
                "Rooms/Props/Other_Rooms/ShelfConjunto.prefab",
                "Rooms/Props/Other_Rooms/LockerConjunto.prefab",
                "Rooms/Props/Stage/SM_Pallet_V3.prefab",
                "Rooms/Props/Hallway/SM_Big_Bench.prefab",
            };

            float x = 38.5f;
            foreach (string piece in pieces)
            {
                var go = _kit.PlacePrefab(root, Prefabs + piece, new Vector3(x, 0f, -22f));
                if (go == null) continue;

                var bounds = LevelToolkitScanner.RendererBounds(go);
                bool collider = go.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger);
                _kit.Label(labels, $"<b>{_kit.CodeOf(Prefabs + piece)}</b>\n<size=65%>{GymBuildKit.NameOf(piece)}\n{LevelToolkitScanner.FormatSize(bounds.size)}\n{(collider ? "com colisor" : "<color=#ff8a7a>SEM colisor</color>")}</size>",
                    new Vector3(x, 0.02f, -18.6f), GymBuildKit.OnFloor(Vector3.back), 1.8f, 2.6f);
                x += 2.7f;
            }

            Sector(root, "Setor S5", new Vector3(48f, 0f, -12f), 7f);
        }

        // ---------------------------------------------------------------- monster & systems

        private static MonsterBrain BuildMonster(Transform home)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Monster/Monster.prefab");
            var monsterRoot = _kit.Group("GYM_Monstro").transform;
            var sectorRoot = _kit.Group("Setores de patrulha", monsterRoot).transform;

            foreach (var sector in Sectors) sector.SetParent(sectorRoot, true);

            var monster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, monsterRoot);
            monster.transform.SetPositionAndRotation(home.position, home.rotation);

            var wander = monster.GetComponent<MonsterWander>();
            var so = new SerializedObject(wander);
            var array = so.FindProperty("allSectors");
            array.arraySize = Sectors.Count;
            for (int i = 0; i < Sectors.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = Sectors[i].GetComponent<PatrolSector>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return monster.GetComponent<MonsterBrain>();
        }

        private static void BuildSystems(Transform spawn, MonsterBrain monster, Transform home)
        {
            var systems = _kit.Group("GYM_Sistemas").transform;

            // Same settings as the NetworkManager in Netbootstrap, with two differences:
            // - it sits at the scene root: Netcode refuses to start a nested NetworkManager;
            // - no scene management: the GYM is a single host-only scene, and with it on Netcode
            //   stops every Play with a modal asking to add the scene to the build list.
            var managerGo = new GameObject("NetworkManager (GYM)");
            var transport = managerGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);
            var manager = managerGo.AddComponent<NetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.TickRate = 30;
            manager.NetworkConfig.ForceSamePrefabs = true;
            manager.NetworkConfig.PlayerPrefab = null;
            manager.RunInBackground = true;

            var prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (prefabList != null && !manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(prefabList))
            {
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            }

            var bootstrapGo = new GameObject("GYM Bootstrap");
            bootstrapGo.transform.SetParent(systems, false);
            var bootstrap = bootstrapGo.AddComponent<GymNetworkBootstrap>();
            var bso = new SerializedObject(bootstrap);
            bso.FindProperty("localNetworkManager").objectReferenceValue = manager;
            bso.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Player/Player.prefab");
            bso.FindProperty("spawnPoint").objectReferenceValue = spawn;
            bso.ApplyModifiedPropertiesWithoutUndo();

            var remote = bootstrapGo.AddComponent<GymMonsterRemote>();
            var rso = new SerializedObject(remote);
            rso.FindProperty("monster").objectReferenceValue = monster;
            rso.FindProperty("home").objectReferenceValue = home;
            rso.ApplyModifiedPropertiesWithoutUndo();

            bootstrapGo.AddComponent<GymDebugHud>();

            // Sabotage: the monster needs an audience to unlock it and a list of things to sabotage.
            var audience = bootstrapGo.AddComponent<GymAudienceProvider>();
            var sabotage = new SerializedObject(monster.GetComponent<MonsterSabotage>());
            sabotage.FindProperty("audienceProviderSource").objectReferenceValue = audience;
            var targets = sabotage.FindProperty("allSabotageObjects");
            var sabotageables = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b is Monster.MonsterSabotages.ISabotageable)
                .Select(b => b.gameObject)
                .Distinct()
                .ToList();
            targets.arraySize = sabotageables.Count;
            for (int i = 0; i < sabotageables.Count; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = sabotageables[i];
            sabotage.ApplyModifiedPropertiesWithoutUndo();

            // Present but switched off: the player's UI asks VivoxManager.instance and logs an error
            // every time it finds none. Disabled, it never joins a channel (no Start/OnEnable).
            var voiceGo = new GameObject("VivoxManager (desligado no GYM)");
            voiceGo.transform.SetParent(systems, false);
            voiceGo.AddComponent<Network.VivoxManager>().enabled = false;

            var sfx = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Sound/SfxManager.prefab");
            if (sfx != null) PrefabUtility.InstantiatePrefab(sfx, systems);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(systems, false);
            cameraGo.transform.SetPositionAndRotation(spawn.position + new Vector3(-6f, 4f, 0f), Quaternion.Euler(25f, 90f, 0f));
            cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CinemachineBrain>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(systems, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var profile = GameVolumeProfile();
            if (profile != null)
            {
                var volumeGo = new GameObject("Global Volume (do Game)");
                volumeGo.transform.SetParent(systems, false);
                var volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            var navMeshGo = new GameObject("NavMesh");
            navMeshGo.transform.SetParent(systems, false);
            var surface = navMeshGo.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = LayerMask.GetMask("Ground", "Interactable", "Scenary", "MainStage", "Recordable", "Walls");
        }

        /// <summary>The post-processing the Game scene uses, so the GYM looks like the game.</summary>
        private static VolumeProfile GameVolumeProfile()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            try
            {
                var volume = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.isGlobal);
                var profile = volume != null ? volume.sharedProfile : null;
                return profile != null && AssetDatabase.Contains(profile) ? profile : null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void EnsureNetworked(GameObject door)
        {
            if (door == null || door.GetComponentInParent<NetworkObject>(true) != null) return;
            door.AddComponent<NetworkObject>();
        }

        private static void BuildNavMesh()
        {
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) return;

            surface.BuildNavMesh();

            LevelToolkitScanner.EnsureFolder(Path.GetDirectoryName(NavMeshDataPath)?.Replace('\\', '/'));
            AssetDatabase.DeleteAsset(NavMeshDataPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
            EditorUtility.SetDirty(surface);
        }
    }
}
