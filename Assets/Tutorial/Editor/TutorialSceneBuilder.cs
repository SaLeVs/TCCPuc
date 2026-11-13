using System.Collections.Generic;
using System.IO;
using System.Linq;
using Audience;
using Components;
using Enums;
using Missions;
using Missions.Donations;
using Missions.Puzzles;
using Monster;
using Monster.MonsterSabotages;
using Objects;
using Objects.PickupItems;
using ScriptableObjects;
using Systems;
using TMPro;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Tutorial.Building
{
    /// <summary>
    /// Gera Assets/Tutorial/Scenes/Game0_Tutorial.unity do zero: onze salas em linha, uma por lição,
    /// separadas por portões que o diretor abre, mais o corredor da fuga. As peças interativas são os
    /// prefabs do jogo (portas, pilha, caixas, objetos de audiência, puzzle das caixas, palco, monstro,
    /// quadro de luz e os managers). Paredes e piso são caixas com os materiais do jogo e UV em metros.
    /// </summary>
    public static class TutorialSceneBuilder
    {
        public const string SceneFolder = "Assets/Tutorial/Scenes";
        public const string ScenePath = SceneFolder + "/" + TutorialLauncher.SceneName + ".unity";
        private const string DataFolder = "Assets/Tutorial/Data";
        private const string GeneratedFolder = "Assets/Tutorial/Generated";
        private const string MeshAssetPath = GeneratedFolder + "/TutorialMeshes.asset";
        private const string NavMeshPath = SceneFolder + "/" + TutorialLauncher.SceneName + "/NavMesh-Tutorial.asset";
        private const string Prefabs = "Assets/Prefabs/";

        private const float Height = 3.4f;
        private const float StageHeight = 5f;
        private const float Thickness = 0.3f;
        private const float GateWidth = 2.2f;
        private const float GateHeight = 2.6f;
        private const float DoorLintel = 2.15f;

        private static int _layerGround, _layerWalls, _layerScenary, _layerInteractable, _layerIgnoreRaycast, _layerDefault;

        private static Material _floor, _wall, _ceiling, _stageFloor, _stageWall, _stage, _wood, _metal, _dark, _curtain, _carpet;
        private static Material _unlitYellow, _unlitGreen, _unlitRed, _unlitBlack, _gateLamp;
        private static TMP_FontAsset _font;

        private static readonly Dictionary<string, Mesh> Meshes = new();
        private static readonly List<GameObject> Shutters = new();
        private static readonly List<PatrolSector> Sectors = new();
        private static readonly Dictionary<string, Object> Refs = new();

        [MenuItem("Tools/TCC/Tutorial/Gerar cena do tutorial", priority = 10)]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Tutorial", "Saia do Play Mode antes de gerar o tutorial.", "Ok");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Tutorial", "A cena do tutorial já existe e será gerada de novo do zero (mudanças feitas à mão nela se perdem). Continuar?", "Gerar de novo", "Cancelar"))
            {
                return;
            }

            Debug.Log(Build());
        }

        /// <summary>Sem diálogos, para automação. Recusa se alguma cena aberta tiver mudança não salva.</summary>
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Saia do Play Mode antes.";

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (open.isDirty && open.path != ScenePath) return $"A cena {open.name} tem mudanças não salvas.";
            }

            EnsureFolder(SceneFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(GeneratedFolder);

            ResolveLayers();
            LoadMaterials();
            Meshes.Clear();
            Shutters.Clear();
            Sectors.Clear();
            Refs.Clear();

            VolumeProfile profile = GameVolumeProfile();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();

            Transform world = Group("TUTORIAL").transform;
            Transform labels = Group("TEXTOS").transform;

            Transform spawn = BuildMovement(world, labels);
            BuildCrouch(world, labels);
            BuildDark(world, labels);
            BuildChat(world, labels);
            BuildItems(world, labels);
            BuildDoors(world, labels);
            BuildAudience(world, labels);
            BuildPuzzle(world, labels);
            Transform home = BuildStage(world, labels);
            BuildEscape(world, labels);

            MonsterBrain monster = BuildMonster(home);
            BuildSystems(spawn, monster, home, profile);

            SaveMeshes();
            BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();

            return $"Tutorial gerado em {ScenePath}.";
        }

        // ================================================================== salas

        /// <summary>Sala 0 (spawn, andar) e o corredor de correr.</summary>
        private static Transform BuildMovement(Transform world, Transform labels)
        {
            Transform room = Group("1 · Movimento", world).transform;

            Room(room, -5f, 5f, 0f, 10f, Height, _floor, _wall);
            WallZ(room, -5f, 0f, 10f, Height);
            WallZ(room, 5f, 0f, 10f, Height);
            WallX(room, 0f, -5f, 5f, Height);
            WallX(room, 10f, -5f, 5f, Height, Gap(0f));
            Gate(room, TutorialDirector.GateRun, new Vector3(0f, 0f, 10f), Vector3.forward);

            Transform spawn = Marker(room, "Spawn", new Vector3(0f, 0.05f, 2f), Vector3.forward);

            Box(room, "Marca amarela", new Vector3(0f, 0f, 7.5f), new Vector3(1.8f, 0.012f, 1.8f), _unlitYellow, _layerDefault, false);
            Zone(room, TutorialDirector.ZoneWalkMark, new Vector3(0f, 0f, 7.5f), new Vector3(2.2f, 2.5f, 2.2f));
            Label(labels, "ANDE ATÉ AQUI", new Vector3(0f, 0.03f, 6.1f), OnFloor(Vector3.forward), 2.4f, 4f, new Color(1f, 0.85f, 0.3f));

            Sign(labels, "1 · MOVIMENTO", "andar · correr · agachar", 10f, 0f, Height);
            Lamp(room, new Vector3(0f, 0f, 5f), Height);

            // Corredor de correr.
            Transform run = Group("1b · Corredor de correr", world).transform;
            Room(run, -1.5f, 1.5f, 10f, 36f, Height, _floor, _wall);
            WallZ(run, -1.5f, 10f, 36f, Height);
            WallZ(run, 1.5f, 10f, 36f, Height);
            WallX(run, 36f, -4f, 4f, Height, Gap(0f));
            Gate(run, TutorialDirector.GateCrouch, new Vector3(0f, 0f, 36f), Vector3.forward);
            Zone(run, TutorialDirector.ZoneRunEnd, new Vector3(0f, 0f, 34.2f), new Vector3(3f, 2.5f, 3.4f));
            Box(run, "Faixa de chegada", new Vector3(0f, 0f, 33f), new Vector3(2.8f, 0.012f, 0.25f), _unlitYellow, _layerDefault, false);
            Label(labels, "CORRA ATÉ AQUI", new Vector3(0f, 0.03f, 32.2f), OnFloor(Vector3.forward), 1.8f, 3f, new Color(1f, 0.85f, 0.3f));

            for (float z = 14f; z < 36f; z += 7f) Lamp(run, new Vector3(0f, 0f, z), Height);

            Box(run, "Tapete", new Vector3(0f, 0f, 23f), new Vector3(1.6f, 0.01f, 24f), _carpet, _layerDefault, false);

            return spawn;
        }

        /// <summary>Sala 2: o cano a 1,3 m, a mesa a 1,08 m e o duto de 0,8 m que nem agachado passa.</summary>
        private static void BuildCrouch(Transform world, Transform labels)
        {
            Transform room = Group("2 · Agachar", world).transform;

            Room(room, -4f, 4f, 36f, 52f, Height, _floor, _wall);
            WallZ(room, -4f, 36f, 52f, Height);
            WallZ(room, 4f, 36f, 52f, Height);
            WallX(room, 52f, -5f, 5f, Height, Gap(0f));
            Gate(room, TutorialDirector.GateDark, new Vector3(0f, 0f, 52f), Vector3.forward);

            // Cano: fundo a 1,30 m. Em pé (1,43 m) não passa; agachado (≈1,15 m) passa.
            GameObject beam = Box(room, "Cano (1,30 m)", new Vector3(0f, 1.30f, 40.84f), new Vector3(8f, 0.32f, 0.32f), _metal, _layerWalls);
            beam.GetComponent<MeshRenderer>().enabled = false;
            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pipe.GetComponent<Collider>());
            pipe.name = "Cano (visual)";
            pipe.transform.SetParent(beam.transform, false);

            // Cilindro padrão: 2 m de altura no Y e 1 m de diâmetro. Deitado no X, 8 m × 0,32 m.
            pipe.transform.localPosition = Vector3.zero;
            pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            pipe.transform.localScale = new Vector3(0.32f, 4f, 0.32f);
            pipe.GetComponent<MeshRenderer>().sharedMaterial = _metal;

            Label(labels, "CANO · 1,3 m\n<size=70%>passa agachado</size>", new Vector3(0f, 1.95f, 40.6f), FacingViewer(Vector3.back), 1.6f, 5f, Color.white);
            Zone(room, TutorialDirector.ZoneCrouchBeam, new Vector3(0f, 0f, 42.6f), new Vector3(8f, 2.5f, 1.8f));

            // Mesa: o vão de baixo tem 1,08 m. Só passa agachado e com o chapéu amassado.
            const float tableTop = 1.08f;
            Box(room, "Mesa (tampo)", new Vector3(0f, tableTop, 46f), new Vector3(8f, 0.1f, 1.6f), _wood, _layerScenary);
            foreach (float x in new[] { -3.8f, 3.8f })
            foreach (float z in new[] { 45.35f, 46.65f })
            {
                Box(room, "Mesa (perna)", new Vector3(x, 0f, z), new Vector3(0.12f, tableTop, 0.12f), _wood, _layerScenary);
            }

            Label(labels, "MESA · 1 m\n<size=70%>agachado, o chapéu amassa</size>", new Vector3(0f, 1.75f, 45.15f), FacingViewer(Vector3.back), 1.6f, 6f, Color.white);
            Zone(room, TutorialDirector.ZoneCrouchTable, new Vector3(0f, 0f, 49.3f), new Vector3(8f, 2.5f, 2.2f));

            // Duto na parede: só mostra o limite (é um painel, não tem passagem).
            Box(room, "Duto 0,8 m (moldura)", new Vector3(-3.83f, 0f, 43.5f), new Vector3(0.06f, 0.9f, 1.5f), _metal, _layerWalls);
            Box(room, "Duto 0,8 m (boca)", new Vector3(-3.79f, 0.05f, 43.5f), new Vector3(0.04f, 0.75f, 1.3f), _unlitBlack, _layerDefault, false);
            Label(labels, "DUTO · 0,8 m\n<size=70%>nem agachado: é o limite</size>", new Vector3(-3.76f, 1.35f, 43.5f), FacingViewer(Vector3.right), 1.3f, 3f, new Color(1f, 0.55f, 0.45f));

            Sign(labels, "2 · PASSAR POR BAIXO", "segure agachar", 52f, 0f, Height);
            Lamp(room, new Vector3(0f, 0f, 38.5f), Height);
            Lamp(room, new Vector3(0f, 0f, 44f), Height);
            Lamp(room, new Vector3(0f, 0f, 50f), Height);
        }

        /// <summary>Sala 3: escura, só a lanterna. A saída é o portão com a luz verde, no canto.</summary>
        private static void BuildDark(Transform world, Transform labels)
        {
            Transform room = Group("3 · Lanterna (escuro)", world).transform;

            Room(room, -5f, 5f, 52f, 66f, Height, _floor, _wall);
            WallZ(room, -5f, 52f, 66f, Height);
            WallZ(room, 5f, 52f, 66f, Height);
            WallX(room, 66f, -5f, 5f, Height, Gap(3f));
            Gate(room, TutorialDirector.GateChat, new Vector3(3f, 0f, 66f), Vector3.forward);

            Zone(room, TutorialDirector.ZoneDarkRoom, new Vector3(0f, 0f, 53.8f), new Vector3(10f, 2.5f, 2.4f));

            Box(room, "Caixote", new Vector3(-2f, 0f, 57f), new Vector3(1.2f, 1.2f, 1.2f), _wood, _layerScenary);
            Box(room, "Caixote", new Vector3(-2.1f, 1.2f, 57.1f), new Vector3(0.8f, 0.8f, 0.8f), _wood, _layerScenary);
            Box(room, "Caixotes", new Vector3(2f, 0f, 59.5f), new Vector3(2.4f, 1.1f, 1.1f), _wood, _layerScenary);
            Box(room, "Estante", new Vector3(-3.6f, 0f, 62.5f), new Vector3(1.6f, 2.2f, 0.6f), _wood, _layerScenary);
            Box(room, "Caixote", new Vector3(0.5f, 0f, 63.2f), new Vector3(1f, 0.9f, 1f), _wood, _layerScenary);

            Sign(labels, "3 · LANTERNA", "procure a saída", 66f, -1.5f, Height);
        }

        /// <summary>Sala 4: o chat. O botão escondido fica atrás do armário grande.</summary>
        private static void BuildChat(Transform world, Transform labels)
        {
            Transform room = Group("4 · Chat", world).transform;

            Room(room, -5f, 5f, 66f, 80f, Height, _floor, _wall);
            WallZ(room, -5f, 66f, 80f, Height);
            WallZ(room, 5f, 66f, 80f, Height);
            WallX(room, 80f, -5f, 5f, Height, Gap(2.5f));
            Gate(room, TutorialDirector.GateItems, new Vector3(2.5f, 0f, 80f), Vector3.forward);

            Zone(room, TutorialDirector.ZoneDarkExit, new Vector3(3f, 0f, 67.6f), new Vector3(3f, 2.5f, 2.2f));

            // Armário: de costas para a parede do fundo com um vão de 0,9 m, aberto pela direita.
            Box(room, "Armário grande", new Vector3(-3.5f, 0f, 78.25f), new Vector3(2.4f, 2.6f, 0.8f), _wood, _layerScenary);
            Box(room, "Sofá", new Vector3(3.2f, 0f, 71f), new Vector3(1f, 0.8f, 2.6f), _carpet, _layerScenary);
            Box(room, "Mesinha", new Vector3(1.4f, 0f, 71f), new Vector3(0.8f, 0.5f, 1.2f), _wood, _layerScenary);

            TutorialButton button = Button(room, new Vector3(-3.7f, 1.25f, 80f - Thickness * 0.5f - 0.02f), Vector3.back);
            Refs["hiddenButton"] = button;

            Sign(labels, "4 · CHAT", "o chat te ajuda", 80f, -1f, Height);
            Lamp(room, new Vector3(0f, 0f, 70f), Height);
            Lamp(room, new Vector3(0f, 0f, 76f), Height);
        }

        /// <summary>Sala 5: a pilha na mesa e a caixa que não é sua.</summary>
        private static void BuildItems(Transform world, Transform labels)
        {
            Transform room = Group("5 · Itens", world).transform;

            Room(room, -5f, 5f, 80f, 92f, Height, _floor, _wall);
            WallZ(room, -5f, 80f, 92f, Height);
            WallZ(room, 5f, 80f, 92f, Height);
            WallX(room, 92f, -5f, 5f, Height, Gap(0f));
            Gate(room, TutorialDirector.GateDoors, new Vector3(0f, 0f, 92f), Vector3.forward);

            const float tableTop = 0.85f;
            Box(room, "Mesa", new Vector3(0f, 0f, 86.5f), new Vector3(2.4f, tableTop, 1f), _wood, _layerScenary);

            GameObject battery = PlaceOnFloor(room, Prefabs + "Items/Pickables/FlashlightBatteryPickable.prefab", new Vector3(0f, tableTop, 86.5f), 90f);
            Refs["battery"] = battery != null ? battery.GetComponent<ItemPickable>() : null;

            const float crateTop = 0.75f;
            Box(room, "Caixote", new Vector3(2.7f, 0f, 86.5f), new Vector3(0.9f, crateTop, 0.9f), _wood, _layerScenary);

            // Caixa de missão sem puzzle ligado: o filtro de missão recusa todo mundo (INDISPONÍVEL).
            GameObject box = PlaceOnFloor(room, Prefabs + "Items/Pickables/Missions/Box/BoxSquarePickable.prefab", new Vector3(2.7f, crateTop, 86.5f), 0f);
            Refs["blockedBox"] = box;

            Label(labels, "PILHA", new Vector3(0f, 0.03f, 85.6f), OnFloor(Vector3.forward), 1.6f, 2f, new Color(1f, 0.85f, 0.3f));
            Sign(labels, "5 · ITENS E BATERIA", "pegar · segurar · usar", 92f, -2.5f, Height);
            Lamp(room, new Vector3(0f, 0f, 85f), Height);
            Lamp(room, new Vector3(0f, 0f, 90f), Height);
        }

        /// <summary>Sala 6: duas portas na parede do fundo. A da esquerda está presa pelo "monstro".</summary>
        private static void BuildDoors(Transform world, Transform labels)
        {
            Transform room = Group("6 · Portas", world).transform;

            Room(room, -5f, 5f, 92f, 102f, Height, _floor, _wall);
            WallZ(room, -5f, 92f, 102f, Height);
            WallZ(room, 5f, 92f, 102f, Height);

            GameObject left = DoorInWallX(room, Prefabs + "Rooms/Props/Doors/Door_Black.prefab", -2.5f, 102f, out float leftWidth);
            GameObject right = DoorInWallX(room, Prefabs + "Rooms/Props/Doors/Door_Black.prefab", 2.5f, 102f, out float rightWidth);

            WallX(room, 102f, -6f, 6f, Height,
                new Opening(-2.5f, leftWidth + 0.02f, DoorLintel),
                new Opening(2.5f, rightWidth + 0.02f, DoorLintel));

            Refs["lockedDoor"] = left != null ? left.GetComponent<Door>() : null;
            Refs["openDoor"] = right != null ? right.GetComponent<Door>() : null;

            Label(labels, "TRAVADA?", new Vector3(-2.5f, 2.6f, 102f - Thickness * 0.5f - 0.02f), FacingViewer(Vector3.back), 1.3f, 2.5f, new Color(1f, 0.55f, 0.45f));
            Label(labels, "ABERTA", new Vector3(2.5f, 2.6f, 102f - Thickness * 0.5f - 0.02f), FacingViewer(Vector3.back), 1.3f, 2.5f, new Color(0.5f, 1f, 0.6f));
            Label(labels, "<b>6 · PORTAS</b>", new Vector3(0f, 2.9f, 102f - Thickness * 0.5f - 0.02f), FacingViewer(Vector3.back), 2.2f, 4f, new Color(1f, 0.85f, 0.35f));

            Lamp(room, new Vector3(0f, 0f, 96f), Height);
        }

        /// <summary>Sala 7: três objetos de audiência em pedestais.</summary>
        private static void BuildAudience(Transform world, Transform labels)
        {
            Transform room = Group("7 · Audiência", world).transform;

            Room(room, -6f, 6f, 102f, 118f, Height, _floor, _wall);
            WallZ(room, -6f, 102f, 118f, Height);
            WallZ(room, 6f, 102f, 118f, Height);
            WallX(room, 118f, -6f, 6f, Height, Gap(0f));
            Gate(room, TutorialDirector.GatePuzzle, new Vector3(0f, 0f, 118f), Vector3.forward);

            Zone(room, TutorialDirector.ZoneDoorsAfter, new Vector3(3f, 0f, 104.4f), new Vector3(6f, 2.5f, 4.4f));

            string[] props =
            {
                "Audience_Objects/SM_Crown.prefab",
                "Audience_Objects/Sword.prefab",
                "Audience_Objects/Sad_Mask_Low.prefab",
            };

            float[] xs = { -3.5f, 0f, 3.5f };
            List<GameObject> placed = new();
            const float pedestal = 1.05f;

            for (int i = 0; i < props.Length; i++)
            {
                Box(room, "Pedestal", new Vector3(xs[i], 0f, 112.5f), new Vector3(0.7f, pedestal, 0.7f), _dark, _layerScenary);
                GameObject prop = PlaceOnFloor(room, Prefabs + props[i], new Vector3(xs[i], pedestal, 112.5f), 180f);
                if (prop != null) placed.Add(prop);
            }

            AudienceProps = placed.ToArray();

            Box(room, "Tapete", new Vector3(0f, 0f, 110f), new Vector3(9f, 0.01f, 6f), _carpet, _layerDefault, false);

            Sign(labels, "7 · AUDIÊNCIA", "grave o que tem olho", 118f, -2.5f, Height);
            Lamp(room, new Vector3(-3f, 0f, 107f), Height);
            Lamp(room, new Vector3(3f, 0f, 107f), Height);
            Lamp(room, new Vector3(0f, 0f, 113.5f), Height);
        }

        private static GameObject[] AudienceProps;

        /// <summary>Sala 8: o puzzle das caixas de verdade (MissionTotemGroup), com os pontos remontados.</summary>
        private static void BuildPuzzle(Transform world, Transform labels)
        {
            Transform room = Group("8 · Puzzle", world).transform;

            Room(room, -6f, 6f, 118f, 134f, Height, _floor, _wall);
            WallZ(room, -6f, 118f, 134f, Height);
            WallZ(room, 6f, 118f, 134f, Height);
            // Alta como o palco: do lado de lá o teto está a 5 m.
            WallX(room, 134f, -9f, 9f, StageHeight, Gap(0f));
            Gate(room, TutorialDirector.GateStage, new Vector3(0f, 0f, 134f), Vector3.forward);

            Zone(room, TutorialDirector.ZonePuzzleRoom, new Vector3(0f, 0f, 120f), new Vector3(12f, 2.5f, 2.6f));

            GameObject group = PlaceAt(room, Prefabs + "Missions/Totems/Box/MissionTotemGroup.prefab", new Vector3(0f, 0f, 126f), 0f);

            if (group != null)
            {
                ItemSlotPuzzleManager manager = group.GetComponent<ItemSlotPuzzleManager>();
                Refs["puzzle"] = manager;
                Refs["puzzleSelector"] = group.GetComponent<MissionOwnershipSelector>();

                Vector3 center = new Vector3(0f, 0f, 127f);
                float totemLift = PivotLift(FirstPrefab(manager, "totemConfigs"));
                float boxLift = PivotLift(FirstPrefab(manager, "pickableConfigs"));

                Vector3[] totems =
                {
                    new(-5.3f, 0f, 123f), new(-5.3f, 0f, 128f), new(5.3f, 0f, 123f), new(5.3f, 0f, 128f), new(0f, 0f, 133.3f),
                };

                for (int i = 0; i < totems.Length; i++)
                {
                    Transform point = group.transform.Find($"TotemsSpawnPoints/TotemBox{i + 1}");
                    if (point == null) continue;

                    point.position = totems[i] + Vector3.up * totemLift;
                    Vector3 toCenter = center - totems[i];
                    toCenter.y = 0f;
                    point.rotation = Quaternion.LookRotation(toCenter.normalized);
                }

                Vector3[] boxes = { new(-2.2f, 0f, 121.5f), new(2.2f, 0f, 121.5f), new(-1.2f, 0f, 130f), new(1.2f, 0f, 130f) };

                for (int i = 0; i < boxes.Length; i++)
                {
                    Transform point = group.transform.Find($"BoxSpawnPoints/Box{i + 1}");
                    if (point == null) continue;

                    point.position = boxes[i] + Vector3.up * boxLift;
                    point.rotation = Quaternion.Euler(0f, 15f * i, 0f);
                }
            }

            Sign(labels, "8 · MISSÃO E PUZZLE", "leia · interprete · resolva", 134f, -3f, Height);
            Lamp(room, new Vector3(-3f, 0f, 122f), Height);
            Lamp(room, new Vector3(3f, 0f, 122f), Height);
            Lamp(room, new Vector3(-3f, 0f, 129.5f), Height);
            Lamp(room, new Vector3(3f, 0f, 129.5f), Height);
        }

        /// <summary>Sala 9: o palco com o monstro congelado, o MainStage e o quadro de luz.</summary>
        private static Transform BuildStage(Transform world, Transform labels)
        {
            Transform room = Group("9 · Palco", world).transform;

            Room(room, -9f, 9f, 134f, 160f, StageHeight, _stageFloor, _stageWall);
            WallZ(room, -9f, 134f, 160f, StageHeight);
            WallZ(room, 9f, 134f, 160f, StageHeight);
            WallX(room, 160f, -9f, 9f, StageHeight, Gap(7f));
            Gate(room, TutorialDirector.GateFuga, new Vector3(7f, 0f, 160f), Vector3.forward);

            Zone(room, TutorialDirector.ZoneStageRoom, new Vector3(0f, 0f, 135.8f), new Vector3(18f, 2.5f, 2.4f));

            // Palco baixo: 0,45 m. O monstro desce (passo do agente 0,5 m), o jogador não sobe.
            const float stageTop = 0.45f;
            Box(room, "Palco", new Vector3(0f, 0f, 154.25f), new Vector3(12f, stageTop, 8.5f), _stage, _layerGround);
            Box(room, "Cortina", new Vector3(0f, stageTop, 159.6f), new Vector3(12f, StageHeight - stageTop - 0.6f, 0.15f), _curtain, _layerWalls);

            Transform home = Marker(room, "Monstro (casa)", new Vector3(0f, stageTop, 154.5f), Vector3.back);

            // O sensor de visão do jogador só vê o que está numa faixa de 1,3 m em volta da mira. O
            // gatilho do palco fica na altura do corpo do monstro: quem enquadra o monstro pega os dois.
            GameObject mainStage = PlaceAt(room, Prefabs + "Audience_Objects/MainStage.prefab", new Vector3(0f, 1.55f, 153.4f), 0f);
            if (mainStage != null) mainStage.name = "MainStage";

            // "Procure marcas no chão": a marca de onde gravar, a uns 7 m do monstro.
            Box(room, "Marca (fita 1)", new Vector3(0f, 0f, 147.5f), new Vector3(1.6f, 0.012f, 0.18f), _unlitYellow, _layerDefault, false)
                .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box(room, "Marca (fita 2)", new Vector3(0f, 0f, 147.5f), new Vector3(1.6f, 0.012f, 0.18f), _unlitYellow, _layerDefault, false)
                .transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            Label(labels, "MARCA DE GRAVAÇÃO", new Vector3(0f, 0.03f, 146.2f), OnFloor(Vector3.forward), 1.6f, 4f, new Color(1f, 0.85f, 0.3f));

            Label(labels, "PALCO PRINCIPAL", new Vector3(0f, 3.8f, 159.45f), FacingViewer(Vector3.back), 3f, 8f, new Color(1f, 0.85f, 0.35f));

            // Quadro de luz na parede esquerda, de frente para a sala.
            GameObject circuit = PlaceAt(room, Prefabs + "Items/EletricCircuit.prefab", new Vector3(-9f + Thickness * 0.5f + 0.05f, 1.2f, 144f), 90f);
            Refs["circuit"] = circuit != null ? circuit.GetComponent<ElectricCircuit>() : null;
            Label(labels, "QUADRO DE LUZ", new Vector3(-8.8f, 2.6f, 144f), FacingViewer(Vector3.right), 1.4f, 3f, Color.white);

            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -4.5f : 4.5f;
                float z = i < 2 ? 139.5f : 147f;
                Lamp(room, new Vector3(x, 0f, z), StageHeight);
            }

            Lamp(room, new Vector3(-3f, 0f, 154f), StageHeight);
            Lamp(room, new Vector3(3f, 0f, 154f), StageHeight);

            EmergencyLamp(room, new Vector3(-8.4f, 3.8f, 140f));
            EmergencyLamp(room, new Vector3(8.4f, 3.8f, 140f));
            EmergencyLamp(room, new Vector3(-8.4f, 3.8f, 152f));
            EmergencyLamp(room, new Vector3(8.4f, 3.8f, 152f));

            Refs["monsterRelease"] = Marker(room, "Monstro solto aqui", new Vector3(5.5f, 0f, 158f), Vector3.forward);

            Sector(room, "Setor palco", new Vector3(0f, 0f, 146f), 8f);

            Sign(labels, "9 · GRAVAÇÃO", "", 134f, 0f, StageHeight, true);

            return home;
        }

        /// <summary>A fuga: corredor em S com duas portas até a sala da saída.</summary>
        private static void BuildEscape(Transform world, Transform labels)
        {
            Transform root = Group("10 · Fuga", world).transform;

            // Trecho A: x 5,5..8,5, z 160..178, abre para o leste no fim.
            Room(root, 5.5f, 8.5f, 160f, 178f, Height, _floor, _wall);
            WallZ(root, 5.5f, 160f, 178f, Height);
            WallZ(root, 8.5f, 160f, 175f, Height);
            WallX(root, 178f, 5.5f, 8.5f, Height);

            // Trecho B: x 8,5..28,5, z 175..178.
            Room(root, 8.5f, 28.5f, 175f, 178f, Height, _floor, _wall);
            WallX(root, 175f, 8.5f, 28.5f, Height);
            WallX(root, 178f, 8.5f, 25.5f, Height);
            WallZ(root, 28.5f, 175f, 178f, Height);

            GameObject first = DoorInWallZ(root, Prefabs + "Rooms/Props/Doors/Door_Black.prefab", 17f, 176.5f, out float firstWidth);
            WallZ(root, 17f, 175f, 178f, Height, new Opening(176.5f, firstWidth + 0.02f, DoorLintel));

            // Trecho C: x 25,5..28,5, z 178..196.
            Room(root, 25.5f, 28.5f, 178f, 196f, Height, _floor, _wall);
            WallZ(root, 25.5f, 178f, 196f, Height);
            WallZ(root, 28.5f, 178f, 196f, Height);

            GameObject second = DoorInWallX(root, Prefabs + "Rooms/Props/Doors/Door_Black.prefab", 27f, 187f, out float secondWidth);
            WallX(root, 187f, 25.5f, 28.5f, Height, new Opening(27f, secondWidth + 0.02f, DoorLintel));

            EscapeDoors = new[] { first, second }.Where(d => d != null).Select(d => d.GetComponent<Door>()).ToArray();

            // Sala da saída.
            Room(root, 23f, 33f, 196f, 204f, Height, _floor, _wall);
            WallX(root, 196f, 23f, 33f, Height, new Opening(27f, 3f, Height));
            WallX(root, 204f, 23f, 33f, Height);
            WallZ(root, 23f, 196f, 204f, Height);
            WallZ(root, 33f, 196f, 204f, Height);

            Zone(root, TutorialDirector.ZoneFugaStart, new Vector3(7f, 0f, 164f), new Vector3(3f, 2.5f, 2f));
            Zone(root, TutorialDirector.ZoneExit, new Vector3(28f, 0f, 200.5f), new Vector3(10f, 2.5f, 6.5f));
            Refs["fugaRetry"] = Marker(root, "Recomeço da fuga", new Vector3(7f, 0.05f, 163f), Vector3.forward);

            Label(labels, "SAÍDA >>", new Vector3(7f, 2.3f, 177.8f), FacingViewer(Vector3.back), 2.2f, 3f, new Color(0.4f, 1f, 0.5f));
            Label(labels, "<< SAÍDA", new Vector3(28.3f, 2.3f, 176.5f), FacingViewer(Vector3.left), 2.2f, 3f, new Color(0.4f, 1f, 0.5f));
            Label(labels, "<b>SAÍDA</b>", new Vector3(28f, 2.6f, 203.8f), FacingViewer(Vector3.back), 4f, 6f, new Color(0.4f, 1f, 0.5f));
            Box(root, "Faixa verde", new Vector3(28f, 0f, 199f), new Vector3(6f, 0.012f, 0.3f), _unlitGreen, _layerDefault, false);

            Lamp(root, new Vector3(7f, 0f, 165f), Height);
            Lamp(root, new Vector3(7f, 0f, 172f), Height);
            Lamp(root, new Vector3(12.5f, 0f, 176.5f), Height);
            Lamp(root, new Vector3(22f, 0f, 176.5f), Height);
            Lamp(root, new Vector3(27f, 0f, 182f), Height);
            Lamp(root, new Vector3(27f, 0f, 192f), Height);
            Lamp(root, new Vector3(28f, 0f, 200f), Height);

            Sector(root, "Setor fuga A", new Vector3(7f, 0f, 169f), 5f);
            Sector(root, "Setor fuga B", new Vector3(18f, 0f, 176.5f), 6f);
            Sector(root, "Setor fuga C", new Vector3(27f, 0f, 188f), 6f);
        }

        private static Door[] EscapeDoors;

        // ================================================================== monstro e sistemas

        private static MonsterBrain BuildMonster(Transform home)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Monster/Monster.prefab");
            Transform root = Group("MONSTRO").transform;
            Transform sectors = Group("Setores de patrulha", root).transform;

            foreach (PatrolSector sector in Sectors) sector.transform.SetParent(sectors, true);

            GameObject monster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            monster.transform.SetPositionAndRotation(home.position, home.rotation);

            SerializedObject wander = new SerializedObject(monster.GetComponent<MonsterWander>());
            SerializedProperty array = wander.FindProperty("allSectors");
            array.arraySize = Sectors.Count;
            for (int i = 0; i < Sectors.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = Sectors[i];
            wander.ApplyModifiedPropertiesWithoutUndo();

            return monster.GetComponent<MonsterBrain>();
        }

        private static void BuildSystems(Transform spawn, MonsterBrain monster, Transform home, VolumeProfile profile)
        {
            Transform systems = Group("SISTEMAS").transform;

            ContractsSO contract = CreateContract();
            DonationDefinition donation = CreateDonation();
            ViewerPopulationSO viewers = AssetDatabase.LoadAssetAtPath<ViewerPopulationSO>("Assets/Scriptable_objects/Chat/ViewerPopulation.asset");

            // Managers do jogo, os mesmos prefabs da cena Game.
            GameObject missionManager = PlaceAt(systems, Prefabs + "Managers/MissionManager.prefab", Vector3.zero, 0f);
            Set(missionManager.GetComponent<MissionManager>(), "currentContract", contract);

            GameObject audience = PlaceAt(systems, Prefabs + "Managers/AudienceManager.prefab", Vector3.zero, 0f);
            Set(audience.GetComponent<AudienceManager>(), "missionManager", missionManager.GetComponent<MissionManager>());
            SetFloat(audience.GetComponent<AudienceManager>(), "startingAudience", 60f);

            GameObject donations = PlaceAt(systems, Prefabs + "Managers/DonationManager.prefab", Vector3.zero, 0f);
            SerializedObject donationSo = new SerializedObject(donations.GetComponent<DonationManager>());
            SerializedProperty pool = donationSo.FindProperty("donationPool");
            pool.arraySize = 1;
            pool.GetArrayElementAtIndex(0).objectReferenceValue = donation;
            donationSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject tts = PlaceAt(systems, Prefabs + "Managers/DonationAudioManager.prefab", Vector3.zero, 0f);
            tts.SetActive(false);

            GameObject hub = PlaceAt(systems, Prefabs + "Managers/ChatEventHub.prefab", Vector3.zero, 0f);
            SerializedObject hubSo = new SerializedObject(hub.GetComponent<ChatEventHub>());
            hubSo.FindProperty("missionNudge.idleSeconds").floatValue = 100000f;
            hubSo.FindProperty("explorationNudge.idleSeconds").floatValue = 100000f;
            hubSo.ApplyModifiedPropertiesWithoutUndo();

            PlaceAt(systems, Prefabs + "Managers/MissionRegisterManager.prefab", Vector3.zero, 0f);
            PlaceAt(systems, Prefabs + "Sound/SfxManager.prefab", Vector3.zero, 0f);

            // Sabotagem do monstro lê a audiência do jogo.
            SerializedObject sabotage = new SerializedObject(monster.GetComponent<MonsterSabotage>());
            sabotage.FindProperty("audienceProviderSource").objectReferenceValue = audience.GetComponent<AudienceManager>();
            sabotage.FindProperty("allSabotageObjects").arraySize = 0;
            sabotage.ApplyModifiedPropertiesWithoutUndo();

            // NetworkManager próprio (desligado): só acorda no Play direto na cena.
            GameObject managerGo = new GameObject("NetworkManager (tutorial)");
            UnityTransport transport = managerGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);
            NetworkManager networkManager = managerGo.AddComponent<NetworkManager>();
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.TickRate = 30;
            networkManager.NetworkConfig.ForceSamePrefabs = true;
            networkManager.NetworkConfig.PlayerPrefab = null;
            networkManager.RunInBackground = true;

            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (prefabList != null && !networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(prefabList))
            {
                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            }

            managerGo.SetActive(false);

            // Tutorial.
            GameObject tutorial = new GameObject("TUTORIAL (diretor)");
            tutorial.transform.SetParent(systems, false);

            TutorialNetworkBootstrap bootstrap = tutorial.AddComponent<TutorialNetworkBootstrap>();
            SerializedObject bootSo = new SerializedObject(bootstrap);
            bootSo.FindProperty("localNetworkManager").objectReferenceValue = managerGo;
            bootSo.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Player/Player.prefab");
            bootSo.FindProperty("spawnPoint").objectReferenceValue = spawn;
            SerializedProperty deferred = bootSo.FindProperty("activateAfterServices");
            deferred.arraySize = 1;
            deferred.GetArrayElementAtIndex(0).objectReferenceValue = tts;
            bootSo.ApplyModifiedPropertiesWithoutUndo();

            TutorialMonsterRemote remote = tutorial.AddComponent<TutorialMonsterRemote>();
            SerializedObject remoteSo = new SerializedObject(remote);
            remoteSo.FindProperty("monster").objectReferenceValue = monster;
            remoteSo.FindProperty("home").objectReferenceValue = home;
            remoteSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject hudGo = new GameObject("TUTORIAL (tela)");
            hudGo.transform.SetParent(systems, false);
            TutorialHud hud = hudGo.AddComponent<TutorialHud>();
            Set(hud, "font", _font);

            TutorialDirector director = tutorial.AddComponent<TutorialDirector>();
            SerializedObject dir = new SerializedObject(director);
            dir.FindProperty("hud").objectReferenceValue = hud;
            dir.FindProperty("viewers").objectReferenceValue = viewers;
            dir.FindProperty("hiddenButton").objectReferenceValue = Refs.GetValueOrDefault("hiddenButton");
            dir.FindProperty("batteryPickable").objectReferenceValue = Refs.GetValueOrDefault("battery");
            dir.FindProperty("blockedBox").objectReferenceValue = Refs.GetValueOrDefault("blockedBox");
            dir.FindProperty("lockedDoor").objectReferenceValue = Refs.GetValueOrDefault("lockedDoor");
            dir.FindProperty("openDoor").objectReferenceValue = Refs.GetValueOrDefault("openDoor");
            dir.FindProperty("puzzle").objectReferenceValue = Refs.GetValueOrDefault("puzzle");
            dir.FindProperty("puzzleSelector").objectReferenceValue = Refs.GetValueOrDefault("puzzleSelector");
            dir.FindProperty("donationManager").objectReferenceValue = donations.GetComponent<DonationManager>();
            dir.FindProperty("tutorialDonation").objectReferenceValue = donation;
            dir.FindProperty("circuit").objectReferenceValue = Refs.GetValueOrDefault("circuit");
            dir.FindProperty("monsterRemote").objectReferenceValue = remote;
            dir.FindProperty("monsterReleasePoint").objectReferenceValue = Refs.GetValueOrDefault("monsterRelease");
            dir.FindProperty("fugaRetryPoint").objectReferenceValue = Refs.GetValueOrDefault("fugaRetry");

            SerializedProperty props = dir.FindProperty("audienceProps");
            props.arraySize = AudienceProps?.Length ?? 0;
            for (int i = 0; i < props.arraySize; i++) props.GetArrayElementAtIndex(i).objectReferenceValue = AudienceProps[i];

            SerializedProperty escape = dir.FindProperty("escapeDoors");
            if (escape != null)
            {
                escape.arraySize = EscapeDoors?.Length ?? 0;
                for (int i = 0; i < escape.arraySize; i++) escape.GetArrayElementAtIndex(i).objectReferenceValue = EscapeDoors[i];
            }

            dir.ApplyModifiedPropertiesWithoutUndo();

            // Câmera, eventos de UI e pós-processamento.
            GameObject cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(systems, false);
            cameraGo.transform.SetPositionAndRotation(spawn.position + new Vector3(0f, 1.6f, -1f), spawn.rotation);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.05f;
            UniversalAdditionalCameraData cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CinemachineBrain>();

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(systems, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            if (profile != null)
            {
                GameObject volumeGo = new GameObject("Global Volume (do Game)");
                volumeGo.transform.SetParent(systems, false);
                Volume volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            GameObject navMeshGo = new GameObject("NavMesh");
            navMeshGo.transform.SetParent(systems, false);
            NavMeshSurface surface = navMeshGo.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = LayerMask.GetMask("Ground", "Interactable", "Scenary", "MainStage", "Recordable", "Walls");
        }

        private static ContractsSO CreateContract()
        {
            string path = DataFolder + "/TutorialContract.asset";
            ContractsSO contract = AssetDatabase.LoadAssetAtPath<ContractsSO>(path);

            if (contract == null)
            {
                contract = ScriptableObject.CreateInstance<ContractsSO>();
                AssetDatabase.CreateAsset(contract, path);
            }

            contract.contractName = "Tutorial";
            contract.mainMissionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Missions/MissionLaerte.prefab");
            contract.mainMission = AssetDatabase.LoadAssetAtPath<MissionSO>("Assets/Scriptable_objects/Contracts/Main_missions/Mission Laerte.asset");

            // Vazio de propósito: as missões do tutorial são entregues pelo diretor, uma por vez.
            contract.personalMissions = new List<MissionSO>();
            contract.baseRooms = new List<RoomDataSO>();
            contract.lootRooms = new List<RoomDataSO>();
            contract.maxAudience = 1200f;

            EditorUtility.SetDirty(contract);
            return contract;
        }

        private static DonationDefinition CreateDonation()
        {
            string path = DataFolder + "/TutorialDonation.asset";
            DonationDefinition donation = AssetDatabase.LoadAssetAtPath<DonationDefinition>(path);

            if (donation == null)
            {
                donation = ScriptableObject.CreateInstance<DonationDefinition>();
                AssetDatabase.CreateAsset(donation, path);
            }

            DonationDefinition record = AssetDatabase.LoadAssetAtPath<DonationDefinition>("Assets/Scriptable_objects/Missions/Donates/DonateRecord.asset");

            donation.donationId = "tutorial_record";
            donation.displayName = "Grave o monstro";
            donation.message = "Grava o monstro 5 segundinhos pra mim, por favor!";
            donation.icon = record != null ? record.icon : null;
            donation.category = DonationCategory.Recording;
            donation.targetType = RecordableTarget.Monster;
            donation.requiredRecordingSeconds = 5f;
            donation.minAmountMoney = 50f;
            donation.maxAmountMoney = 50f;
            donation.durationSeconds = 0f;
            donation.triggerRule = new DonationTriggerRule
            {
                // Nunca sai sozinha: quem manda é o diretor do tutorial.
                minViewersRequired = int.MaxValue,
                baseChance = 0f,
                cooldownSeconds = 100000f
            };
            donation.fakeDonorNames = AssetDatabase.LoadAssetAtPath<ViewerPopulationSO>("Assets/Scriptable_objects/Chat/ViewerPopulation.asset");

            EditorUtility.SetDirty(donation);
            return donation;
        }

        // ================================================================== peças

        private readonly struct Opening
        {
            public readonly float Center;
            public readonly float Width;
            public readonly float Height;

            public Opening(float center, float width = GateWidth, float height = GateHeight)
            {
                Center = center;
                Width = width;
                Height = height;
            }
        }

        private static Opening Gap(float center) => new Opening(center);

        /// <summary>Piso e teto de uma sala retangular.</summary>
        private static void Room(Transform parent, float x0, float x1, float z0, float z1, float height, Material floor, Material wall)
        {
            Vector3 size = new Vector3(x1 - x0, 0.2f, z1 - z0);
            Vector3 center = new Vector3((x0 + x1) * 0.5f, -0.2f, (z0 + z1) * 0.5f);

            Box(parent, "Piso", center, size, floor, _layerGround);
            Box(parent, "Teto", new Vector3(center.x, height, center.z), new Vector3(size.x, 0.2f, size.z), _ceiling, _layerWalls);

            _lastWall = wall;
        }

        private static Material _lastWall;

        /// <summary>Parede ao longo de X, em z fixo, de x0 a x1, com vãos.</summary>
        private static void WallX(Transform parent, float z, float x0, float x1, float height, params Opening[] openings)
        {
            float cursor = x0;

            foreach (Opening opening in openings.OrderBy(o => o.Center))
            {
                float start = opening.Center - opening.Width * 0.5f;
                float end = opening.Center + opening.Width * 0.5f;

                if (start > cursor + 0.01f) WallBlock(parent, new Vector3((cursor + start) * 0.5f, 0f, z), new Vector3(start - cursor, height, Thickness));

                if (opening.Height < height - 0.01f)
                {
                    WallBlock(parent, new Vector3(opening.Center, opening.Height, z), new Vector3(opening.Width, height - opening.Height, Thickness), "Verga");
                }

                cursor = end;
            }

            if (x1 > cursor + 0.01f) WallBlock(parent, new Vector3((cursor + x1) * 0.5f, 0f, z), new Vector3(x1 - cursor, height, Thickness));
        }

        /// <summary>Parede ao longo de Z, em x fixo, de z0 a z1, com vãos.</summary>
        private static void WallZ(Transform parent, float x, float z0, float z1, float height, params Opening[] openings)
        {
            float cursor = z0;

            foreach (Opening opening in openings.OrderBy(o => o.Center))
            {
                float start = opening.Center - opening.Width * 0.5f;
                float end = opening.Center + opening.Width * 0.5f;

                if (start > cursor + 0.01f) WallBlock(parent, new Vector3(x, 0f, (cursor + start) * 0.5f), new Vector3(Thickness, height, start - cursor));

                if (opening.Height < height - 0.01f)
                {
                    WallBlock(parent, new Vector3(x, opening.Height, opening.Center), new Vector3(Thickness, height - opening.Height, opening.Width), "Verga");
                }

                cursor = end;
            }

            if (z1 > cursor + 0.01f) WallBlock(parent, new Vector3(x, 0f, (cursor + z1) * 0.5f), new Vector3(Thickness, height, z1 - cursor));
        }

        private static void WallBlock(Transform parent, Vector3 bottomCenter, Vector3 size, string name = "Parede")
        {
            Box(parent, name, bottomCenter, size, _lastWall ?? _wall, _layerWalls);
        }

        /// <summary>Portão de correr no vão de uma parede ao longo de X ou Z, com a luz do lado de quem chega.</summary>
        private static void Gate(Transform parent, string id, Vector3 bottomCenter, Vector3 approachFacing)
        {
            GameObject gate = new GameObject($"Portão · {id}");
            gate.transform.SetParent(parent, false);
            gate.transform.localPosition = bottomCenter;
            gate.transform.localRotation = Quaternion.LookRotation(approachFacing);

            GameObject shutter = Box(gate.transform, "Folha", Vector3.zero, new Vector3(GateWidth, GateHeight, 0.16f), _metal, _layerWalls);
            shutter.transform.localPosition = new Vector3(0f, GateHeight * 0.5f, 0f);
            Shutters.Add(shutter);

            GameObject lamp = Box(gate.transform, "Luz", Vector3.zero, new Vector3(0.7f, 0.14f, 0.05f), _gateLamp, _layerDefault, false);
            lamp.transform.localPosition = new Vector3(0f, GateHeight + 0.25f, -Thickness * 0.5f - 0.04f);

            TutorialGate component = gate.AddComponent<TutorialGate>();
            component.Configure(id, shutter.transform, lamp.GetComponent<Renderer>(), false);
        }

        private static void Zone(Transform parent, string id, Vector3 bottomCenter, Vector3 size)
        {
            GameObject zone = new GameObject($"Zona · {id}");
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = bottomCenter + Vector3.up * (size.y * 0.5f);
            zone.layer = _layerIgnoreRaycast;

            BoxCollider trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size;

            zone.AddComponent<TutorialZone>().Configure(id);
        }

        private static TutorialButton Button(Transform parent, Vector3 position, Vector3 facing)
        {
            GameObject root = new GameObject("Botão escondido");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.LookRotation(facing);
            root.layer = _layerInteractable;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.45f, 0.45f, 0.2f);
            collider.center = new Vector3(0f, 0f, 0.06f);

            GameObject plate = Box(root.transform, "Placa", Vector3.zero, new Vector3(0.4f, 0.4f, 0.04f), _metal, _layerInteractable, false);
            plate.transform.localPosition = new Vector3(0f, -0.2f, 0.02f);

            GameObject cap = Box(root.transform, "Botão", Vector3.zero, new Vector3(0.18f, 0.18f, 0.08f), _gateLamp, _layerInteractable, false);
            cap.transform.localPosition = new Vector3(0f, -0.09f, 0.06f);

            Outline outline = cap.AddComponent<Outline>();
            SerializedObject outlineSo = new SerializedObject(outline);
            outlineSo.FindProperty("outlineColor").colorValue = new Color(1f, 0.85f, 0.38f, 1f);
            outlineSo.FindProperty("outlineWidth").floatValue = 4f;
            outlineSo.ApplyModifiedPropertiesWithoutUndo();
            outline.enabled = false;

            Highlight highlight = root.AddComponent<Highlight>();
            Set(highlight, "outline", outline);

            InteractionMarker marker = root.AddComponent<InteractionMarker>();
            SerializedObject markerSo = new SerializedObject(marker);
            markerSo.FindProperty("actionLabel").stringValue = "Apertar";
            markerSo.FindProperty("revealDistance").floatValue = 3f;
            markerSo.ApplyModifiedPropertiesWithoutUndo();

            TutorialButton button = root.AddComponent<TutorialButton>();
            button.Configure("chat_button", cap.transform, cap.GetComponent<Renderer>());
            return button;
        }

        /// <summary>Porta numa parede ao longo de X (folha de frente para Z).</summary>
        private static GameObject DoorInWallX(Transform parent, string path, float x, float z, out float width)
        {
            GameObject door = PlaceAt(parent, path, new Vector3(x, 0f, z), 0f);
            width = 1.2f;
            if (door == null) return null;

            EnsureNetworked(door);
            Bounds bounds = RendererBounds(door);
            width = bounds.size.x;
            door.transform.position += new Vector3(x - bounds.center.x, -bounds.min.y, z - bounds.center.z);
            return door;
        }

        /// <summary>Porta numa parede ao longo de Z (folha de frente para X).</summary>
        private static GameObject DoorInWallZ(Transform parent, string path, float x, float z, out float width)
        {
            GameObject door = PlaceAt(parent, path, new Vector3(x, 0f, z), 90f);
            width = 1.2f;
            if (door == null) return null;

            EnsureNetworked(door);
            Bounds bounds = RendererBounds(door);
            width = bounds.size.z;
            door.transform.position += new Vector3(x - bounds.center.x, -bounds.min.y, z - bounds.center.z);
            return door;
        }

        private static void EnsureNetworked(GameObject go)
        {
            if (go.GetComponentInParent<NetworkObject>(true) == null) go.AddComponent<NetworkObject>();
        }

        /// <summary>Luminária do jogo (com LightSabotage) presa no teto.</summary>
        private static void Lamp(Transform parent, Vector3 floorPoint, float ceiling)
        {
            GameObject lamp = PlaceAt(parent, Prefabs + "Rooms/Props/Hallway/SM_Hallway_Light_Roof.prefab", floorPoint, 0f);
            if (lamp == null) return;

            Bounds bounds = RendererBounds(lamp);
            lamp.transform.position += new Vector3(0f, ceiling - 0.01f - bounds.max.y, 0f);

            // Override só nesta cena: a luminária do jogo (2,6 / 8 m) foi feita para corredor estreito.
            bool tall = ceiling > Height + 0.1f;

            foreach (Light light in lamp.GetComponentsInChildren<Light>(true))
            {
                light.intensity = tall ? 12f : 8f;
                light.range = tall ? 16f : 13f;
                EditorUtility.SetDirty(light);
            }
        }

        /// <summary>Luz vermelha que só acende quando o monstro apaga as luzes.</summary>
        private static void EmergencyLamp(Transform parent, Vector3 position)
        {
            GameObject go = new GameObject("Luz de emergência");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.layer = LayerMask.NameToLayer("Lights") >= 0 ? LayerMask.NameToLayer("Lights") : 0;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.12f, 0.08f);
            light.intensity = 3.5f;
            light.range = 11f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            LightSabotage sabotage = go.AddComponent<LightSabotage>();
            SerializedObject so = new SerializedObject(sabotage);
            so.FindProperty("lightComponent").objectReferenceValue = light;
            so.FindProperty("emergency").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject bulb = Box(go.transform, "Lâmpada", Vector3.zero, new Vector3(0.25f, 0.12f, 0.25f), _unlitRed, _layerDefault, false);
            bulb.transform.localPosition = Vector3.zero;
        }

        private static void Sector(Transform parent, string name, Vector3 position, float radius)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Managers/MapStages/SectorStage.prefab");
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : new GameObject();
            go.name = name;
            go.transform.localPosition = position;

            PatrolSector sector = go.GetComponent<PatrolSector>() ?? go.AddComponent<PatrolSector>();
            SerializedObject so = new SerializedObject(sector);
            SerializedProperty radiusProperty = so.FindProperty("patrolSectorRadius");
            if (radiusProperty != null) radiusProperty.floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();

            Sectors.Add(sector);
        }

        /// <summary>Placa com o número e o nome da lição, na parede do fundo (vista de quem entra).</summary>
        private static void Sign(Transform labels, string title, string subtitle, float wallZ, float x, float wallHeight, bool high = false)
        {
            string text = string.IsNullOrEmpty(subtitle) ? $"<b>{title}</b>" : $"<b>{title}</b>\n<size=60%>{subtitle}</size>";
            float y = high ? 4.1f : Mathf.Min(wallHeight - 0.35f, GateHeight + 0.62f);
            Label(labels, text, new Vector3(x, y, wallZ - Thickness * 0.5f - 0.02f), FacingViewer(Vector3.back), 2.4f, 6f, new Color(1f, 0.85f, 0.35f));
        }

        private static TextMeshPro Label(Transform parent, string text, Vector3 position, Quaternion rotation, float fontSize, float width, Color color)
        {
            GameObject go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            if (_font != null) tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(width, fontSize * 0.3f * (1 + text.Count(c => c == '\n')));
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private static Quaternion FacingViewer(Vector3 towardsViewer) => Quaternion.LookRotation(-towardsViewer.normalized, Vector3.up);

        private static Quaternion OnFloor(Vector3 readingDirection) => Quaternion.LookRotation(Vector3.down, readingDirection.normalized);

        private static Transform Marker(Transform parent, string name, Vector3 position, Vector3 forward)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(forward.normalized);
            return go.transform;
        }

        private static GameObject Group(string name, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        // ================================================================== malha com UV em metros

        /// <summary>Caixa dada pelo centro da base, com UV em metros (a textura não estica).</summary>
        private static GameObject Box(Transform parent, string name, Vector3 bottomCenter, Vector3 size, Material material, int layer, bool collider = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = bottomCenter + Vector3.up * (size.y * 0.5f);
            go.layer = layer;

            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            if (collider)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.size = size;
            }

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private const float UvMeters = 2.5f;

        private static Mesh BoxMesh(Vector3 size)
        {
            string key = $"{size.x:0.###}x{size.y:0.###}x{size.z:0.###}";
            if (Meshes.TryGetValue(key, out Mesh cached)) return cached;

            Vector3 h = size * 0.5f;
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<Vector2> uvs = new();
            List<int> triangles = new();

            void Face(Vector3 normal, Vector3 u, Vector3 v, float width, float height)
            {
                int start = vertices.Count;
                Vector3 center = Vector3.Scale(normal, h);
                Vector3 du = u * (width * 0.5f);
                Vector3 dv = v * (height * 0.5f);

                vertices.Add(center - du - dv);
                vertices.Add(center + du - dv);
                vertices.Add(center + du + dv);
                vertices.Add(center - du + dv);

                for (int i = 0; i < 4; i++) normals.Add(normal);

                float uw = width / UvMeters;
                float vh = height / UvMeters;
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(uw, 0f));
                uvs.Add(new Vector2(uw, vh));
                uvs.Add(new Vector2(0f, vh));

                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }

            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);

            Mesh mesh = new Mesh { name = "Caixa " + key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            Meshes[key] = mesh;
            return mesh;
        }

        private static void SaveMeshes()
        {
            AssetDatabase.DeleteAsset(MeshAssetPath);

            Mesh[] meshes = Meshes.Values.ToArray();
            if (meshes.Length == 0) return;

            AssetDatabase.CreateAsset(meshes[0], MeshAssetPath);
            for (int i = 1; i < meshes.Length; i++) AssetDatabase.AddObjectToAsset(meshes[i], MeshAssetPath);

            AssetDatabase.ImportAsset(MeshAssetPath);
        }

        // ================================================================== prefabs

        private static GameObject PlaceAt(Transform parent, string path, Vector3 position, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogWarning($"Tutorial: prefab não encontrado: {path}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return instance;
        }

        /// <summary>Encosta a base do que se vê do prefab em <paramref name="bottomCenter"/>, qualquer que seja o pivô.</summary>
        private static GameObject PlaceOnFloor(Transform parent, string path, Vector3 bottomCenter, float yaw)
        {
            GameObject instance = PlaceAt(parent, path, Vector3.zero, yaw);
            if (instance == null) return null;

            Bounds bounds = RendererBounds(instance);

            if (bounds.size == Vector3.zero)
            {
                instance.transform.localPosition = bottomCenter;
                return instance;
            }

            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.transform.position += parent.TransformPoint(bottomCenter) - bottom + Vector3.up * 0.002f;
            return instance;
        }

        /// <summary>Quanto o pivô do prefab precisa subir para a base da malha ficar no chão.</summary>
        private static float PivotLift(GameObject prefab)
        {
            if (prefab == null) return 0f;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            try
            {
                Bounds bounds = RendererBounds(instance);
                return bounds.size == Vector3.zero ? 0f : -bounds.min.y + 0.002f;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject FirstPrefab(Object manager, string listName)
        {
            if (manager == null) return null;

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty list = so.FindProperty(listName);
            if (list == null || list.arraySize == 0) return null;

            SerializedProperty prefabs = list.GetArrayElementAtIndex(0).FindPropertyRelative("prefabs");
            if (prefabs == null || prefabs.arraySize == 0) return null;

            return prefabs.GetArrayElementAtIndex(0).objectReferenceValue as GameObject;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Bounds bounds = default;
            bool found = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;

                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;

                found = true;
            }

            return bounds;
        }

        // ================================================================== ambiente

        private static void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.045f, 0.045f, 0.055f);
            RenderSettings.ambientEquatorColor = new Color(0.035f, 0.03f, 0.028f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.011f, 0.01f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.02f, 0.018f, 0.02f);
            RenderSettings.reflectionIntensity = 0.4f;
        }

        private static void BuildNavMesh()
        {
            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) return;

            // Portões abertos no bake: fechados, cortariam o NavMesh entre as salas.
            foreach (GameObject shutter in Shutters) shutter.SetActive(false);

            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                foreach (GameObject shutter in Shutters) shutter.SetActive(true);
            }

            EnsureFolder(Path.GetDirectoryName(NavMeshPath)?.Replace('\\', '/'));
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        private static VolumeProfile GameVolumeProfile()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");

            try
            {
                Volume volume = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>(true)).FirstOrDefault(v => v.isGlobal);
                VolumeProfile profile = volume != null ? volume.sharedProfile : null;
                return profile != null && AssetDatabase.Contains(profile) ? profile : null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================== materiais e utilidades

        private static void ResolveLayers()
        {
            _layerDefault = 0;
            _layerGround = LayerMask.NameToLayer("Ground");
            _layerWalls = LayerMask.NameToLayer("Walls");
            _layerScenary = LayerMask.NameToLayer("Scenary");
            _layerInteractable = LayerMask.NameToLayer("Interactable");
            _layerIgnoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        }

        private static void LoadMaterials()
        {
            // As texturas de cenário do jogo são atlas feitos para um modelo cada (bake do Substance):
            // numa parede reta viram blocos coloridos. Aqui são cores lisas, no clima de bastidor de teatro.
            _floor = Lit("Tutorial_Piso", new Color(0.21f, 0.14f, 0.1f), 0.35f);
            _wall = Lit("Tutorial_Parede", new Color(0.34f, 0.12f, 0.12f), 0.12f);
            _ceiling = Lit("Tutorial_Teto", new Color(0.09f, 0.085f, 0.09f), 0.05f);
            _stageFloor = Lit("Tutorial_PisoPalco", new Color(0.16f, 0.11f, 0.08f), 0.3f);
            _stageWall = Lit("Tutorial_ParedePalco", new Color(0.11f, 0.08f, 0.09f), 0.1f);
            _stage = Lit("Tutorial_Palco", new Color(0.27f, 0.17f, 0.1f), 0.4f);
            _curtain = Lit("Tutorial_Cortina", new Color(0.38f, 0.04f, 0.05f), 0.15f);
            _carpet = Lit("Tutorial_Tapete", new Color(0.3f, 0.06f, 0.07f), 0.05f);
            _metal = Lit("Tutorial_Metal", new Color(0.27f, 0.28f, 0.3f), 0.55f, 0.6f);
            _wood = Lit("Tutorial_Madeira", new Color(0.33f, 0.21f, 0.13f), 0.3f);
            _dark = Lit("Tutorial_Pedestal", new Color(0.07f, 0.06f, 0.06f), 0.4f);

            _unlitYellow = Unlit("Tutorial_Amarelo", new Color(1f, 0.8f, 0.2f), true);
            _unlitGreen = Unlit("Tutorial_Verde", new Color(0.3f, 0.95f, 0.45f), true);
            _unlitRed = Unlit("Tutorial_Vermelho", new Color(1f, 0.15f, 0.1f), true);
            _unlitBlack = Unlit("Tutorial_Preto", new Color(0.01f, 0.01f, 0.01f), true);
            _gateLamp = Unlit("Tutorial_LuzPortao", Color.white, true);

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Acme-Regular SDF.asset");
        }

        private static Material Lit(string name, Color color, float smoothness, float metallic = 0f)
        {
            Material material = Unlit(name, color, false);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Unlit(string name, Color color, bool unlit)
        {
            string path = $"{GeneratedFolder}/{name}.mat";
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Set(Object target, string field, Object value)
        {
            if (target == null) return;

            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"Tutorial: campo {field} não existe em {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            if (target == null) return;

            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) return;

            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
