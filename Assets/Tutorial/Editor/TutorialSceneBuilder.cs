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
    /// Gera Assets/Scenes/Game0_Tutorial.unity do zero: onze salas em linha, uma por lição,
    /// mais o corredor da fuga. Tudo o que se vê é peça do cenário do jogo (paredes, pisos, tetos,
    /// móveis, palco, canos, luminárias) e as passagens entre as salas são portas de verdade do jogo,
    /// trancadas, com a luz de emergência em cima.
    ///
    /// <para>A colisão das paredes e do piso é feita por caixas invisíveis do gerador: as peças de
    /// parede do jogo não têm colisor (no jogo ele fica em outro objeto), e assim a passagem das portas
    /// tem sempre a mesma medida para o jogador e para o NavMesh do monstro.</para>
    /// </summary>
    public static class TutorialSceneBuilder
    {
        // O nome tem que começar com "Game" + dígito: chat, audiência, REC e música só ligam em cenas
        // que SceneNames.IsGame reconhece como partida.
        public const string SceneFolder = "Assets/Scenes";
        public const string ScenePath = SceneFolder + "/" + TutorialLauncher.SceneName + ".unity";
        private const string DataFolder = "Assets/Tutorial/Data";
        private const string GeneratedFolder = "Assets/Tutorial/Generated";
        private const string MeshAssetPath = GeneratedFolder + "/TutorialMeshes.asset";
        private const string NavMeshPath = GeneratedFolder + "/NavMesh-Tutorial.asset";

        private const string Prefabs = "Assets/Prefabs/";
        private const string Props = Prefabs + "Rooms/Props/";
        private const string H = Props + "Hallway/";
        private const string D = Props + "Dressing_Room/";
        private const string B = Props + "Box_Office/";
        private const string O = Props + "Other_Rooms/";
        private const string S = Props + "Stage/";
        private const string W = Props + "Bathroom/";
        private const string A = Props + "Auditorium/";
        private const string Doors = Props + "Doors/";
        private const string PipeModels = "Assets/Models/Missions_objects/";

        // Pisos e tetos (todos visíveis de baixo, conferido em render).
        private const string FloorHall = H + "SM_Unique_Floor_Hallway.prefab";
        private const string FloorTiles = D + "SM_Unique_Floor.prefab";
        private const string FloorPlanks = D + "SM_Unique_Floor_V2.prefab";
        private const string FloorMarble = B + "SM_Foyer_Floor_2x2.prefab";
        private const string RoofHall = H + "SM_Roof_2.prefab";
        private const string RoofRoom = D + "SM_Unique_Roof.prefab";
        private const string RoofFoyer = B + "SM_Foyer_Roof_2x2.prefab";

        private const float Height = 3f;
        private const float StageHeight = 5.5f;
        private const float Thickness = 0.36f;
        private const float DoorWidth = 0.94f;
        private const float DoorLintel = 2.13f;
        private const float WallPieceHeight = 3f;

        private static int _layerGround, _layerWalls, _layerInteractable, _layerIgnoreRaycast, _layerDefault;

        private static Material _metal, _unlitYellow, _unlitGreen, _unlitRed, _unlitBlack, _gateLamp, _emergencyLamp, _exitLamp;
        private static TMP_FontAsset _font;

        private static readonly Dictionary<string, Mesh> Meshes = new();
        private static readonly List<PatrolSector> Sectors = new();
        private static readonly Dictionary<string, Object> Refs = new();
        private static readonly List<Door> EscapeDoors = new();
        private static readonly List<Door> DecorDoors = new();
        private static GameObject[] _audienceProps;

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
                !EditorUtility.DisplayDialog("Tutorial", "A cena do tutorial já existe e será gerada de novo do zero: mudanças feitas à mão nela se perdem, inclusive o bake de luz, o Light Probe Group e o Global Volume que o grupo colocou (2026-11-18). Continuar?", "Gerar de novo", "Cancelar"))
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
            Sectors.Clear();
            Refs.Clear();
            EscapeDoors.Clear();
            DecorDoors.Clear();

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

            // Textos de parede com a placa do teatro (a do ACHADOS E PERDIDOS do jogo).
            TutorialPlaques.ConvertScene(scene);

            SaveMeshes();
            BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            // Segunda passada, agora que a cena tem arquivo e está no Build Settings: só assim o
            // Netcode consegue dar um ID próprio a cada objeto de rede da cena.
            RefreshNetworkIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            string problem = CheckNetworkIds(scene);
            return problem ?? $"Tutorial gerado em {ScenePath}.";
        }

        /// <summary>
        /// O NetworkObject calcula o GlobalObjectIdHash no OnValidate, a partir do GlobalObjectId do
        /// objeto. Numa cena nova, ainda sem arquivo, esse ID não existe e o OnValidate desiste: cada
        /// porta ficava com o hash do prefab, todas iguais. Pelo menu (gerenciamento de cenas do
        /// Netcode ligado) isso derrubava o StartHost ("already contains the same GlobalObjectIdHash").
        /// Aqui o OnValidate roda de novo com a cena salva, e a mudança vira override salvo na cena.
        /// </summary>
        private static void RefreshNetworkIds(Scene scene)
        {
            System.Reflection.MethodInfo validate = typeof(NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            if (validate == null)
            {
                Debug.LogWarning("Tutorial: NetworkObject.OnValidate não foi encontrado; os IDs de rede da cena podem ficar repetidos.");
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    validate.Invoke(networkObject, null);

                    if (PrefabUtility.IsPartOfPrefabInstance(networkObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(networkObject);
                    EditorUtility.SetDirty(networkObject);
                }
            }
        }

        /// <summary>Confere que nenhum objeto de rede da cena repete ID (entre si ou com o do prefab).</summary>
        private static string CheckNetworkIds(Scene scene)
        {
            System.Reflection.FieldInfo hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (hashField == null) return null;

            Dictionary<uint, string> seen = new();
            List<string> problems = new();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    uint hash = (uint)hashField.GetValue(networkObject);

                    if (seen.TryGetValue(hash, out string other)) problems.Add($"{networkObject.name} = {other} ({hash})");
                    else seen[hash] = networkObject.name;

                    NetworkObject source = PrefabUtility.GetCorrespondingObjectFromSource(networkObject);
                    if (source != null && (uint)hashField.GetValue(source) == hash) problems.Add($"{networkObject.name} com o ID do prefab ({hash})");
                }
            }

            return problems.Count == 0 ? null : "Tutorial gerado, MAS com IDs de rede repetidos: " + string.Join("; ", problems);
        }

        // ================================================================== salas

        /// <summary>Sala 1 (saguão: andar) e o corredor de correr.</summary>
        private static Transform BuildMovement(Transform world, Transform labels)
        {
            Transform room = Group("1 · Movimento (saguão)", world).transform;

            Room(room, -5f, 5f, 0f, 10f, Height, FloorMarble, RoofFoyer, true);
            Wall(room, false, -5f, 0f, 10f, Height);
            Wall(room, false, 5f, 0f, 10f, Height);
            Wall(room, true, 0f, -5f, 5f, Height);
            Wall(room, true, 10f, -5f, 5f, Height, DoorGap(0f));
            Gate(room, TutorialDirector.GateRun, 0f, 10f);

            Transform spawn = Marker(room, "Spawn", new Vector3(0f, 0.05f, 2f), Vector3.forward);

            Transform walkHint = FloorHint(labels, TutorialDirector.ZoneWalkMark);
            Box(walkHint, "Marca amarela", new Vector3(0f, 0.004f, 7.2f), new Vector3(1.8f, 0.012f, 1.8f), _unlitYellow, _layerDefault, false);
            Zone(room, TutorialDirector.ZoneWalkMark, new Vector3(0f, 0f, 7.2f), new Vector3(2.2f, 2.5f, 2.2f));
            Label(walkHint, "ANDE ATÉ AQUI", new Vector3(0f, 0.03f, 5.8f), OnFloor(Vector3.forward), 2.4f, 4f, new Color(1f, 0.85f, 0.3f));

            // Saguão do teatro: sofá e poltronas de um lado, pipoca e refrigerante do outro.
            Prop(room, B + "SM_Sofa.prefab", new Vector3(-4.3f, 0f, 4.6f), Face(Vector3.right));
            Prop(room, B + "SM_Chair_Foyer.prefab", new Vector3(-4.3f, 0f, 2.1f), Face(new Vector3(1f, 0f, 0.4f)));
            Prop(room, B + "SM_Chair_Foyer.prefab", new Vector3(-4.3f, 0f, 7.1f), Face(new Vector3(1f, 0f, -0.4f)));
            Prop(room, B + "SM_Popcorn_Machine.prefab", new Vector3(4.4f, 0f, 3.2f), Face(Vector3.left));
            Prop(room, B + "SM_Soda_Machine.prefab", new Vector3(4.4f, 0f, 4.5f), Face(Vector3.left));
            Prop(room, B + "SM_Popcorn_Machine.prefab", new Vector3(4.4f, 0f, 5.8f), Face(Vector3.left));
            Prop(room, W + "SM_Bathroom_Trash_Can.prefab", new Vector3(4.5f, 0f, 7.3f), 0f);

            // Corredor de fila até a marca, como na bilheteria.
            Queue(room, new Vector3(-1.8f, 0f, 2.9f), new Vector3(-1.8f, 0f, 6.3f));
            Queue(room, new Vector3(1.8f, 0f, 2.9f), new Vector3(1.8f, 0f, 6.3f));

            WallProp(room, H + "SM_Poster.prefab", new Vector3(-4.8f, 1.65f, 8.6f), Vector3.right);
            WallProp(room, H + "SM_Poster_V2.prefab", new Vector3(4.8f, 1.65f, 8.6f), Vector3.left);
            WallProp(room, H + "SM_Poster.prefab", new Vector3(-2.6f, 1.6f, 0.2f), Vector3.forward);
            WallProp(room, H + "SM_Poster_V2.prefab", new Vector3(2.6f, 1.6f, 0.2f), Vector3.forward);
            WallProp(room, B + "SM_Snacks_Sign_New.prefab", new Vector3(4.78f, 2.45f, 4.5f), Vector3.left, true);
            WallProp(room, B + "SM_Menu_Board_Big_Popcorn.prefab", new Vector3(4.76f, 1.75f, 2.4f), Vector3.left, true);
            WallProp(room, B + "SM_Menu_Board_Big_Drinks.prefab", new Vector3(4.76f, 1.75f, 6.6f), Vector3.left, true);
            WallProp(room, B + "SM_Tickets_Sign_New.prefab", new Vector3(0f, 2.55f, 0.22f), Vector3.forward, true);
            Prop(room, H + "SM_Carpet_01.prefab", new Vector3(0f, 0.002f, 2.6f), 90f, 1.8f);
            Prop(room, H + "SM_Carpet_01.prefab", new Vector3(0f, 0.002f, 4.6f), 90f, 1.8f);

            Sign(labels, TutorialDirector.SignMovement, "1 · MOVIMENTO", "andar · correr", 10f, -2.9f);
            Lamp(room, new Vector3(-2f, 0f, 3f), Height);
            Lamp(room, new Vector3(2f, 0f, 7f), Height);

            // Corredor de correr.
            Transform run = Group("1b · Corredor de correr", world).transform;
            Room(run, -1.5f, 1.5f, 10f, 36f, Height, FloorHall, RoofHall, true);
            Wall(run, false, -1.5f, 10f, 36f, Height);
            Wall(run, false, 1.5f, 10f, 36f, Height);
            Wall(run, true, 36f, -4f, 4f, Height, DoorGap(0f));
            Gate(run, TutorialDirector.GateCrouch, 0f, 36f);

            Zone(run, TutorialDirector.ZoneRunStart, new Vector3(0f, 0f, 11.4f), new Vector3(3f, 2.5f, 2f));
            Zone(run, TutorialDirector.ZoneRunEnd, new Vector3(0f, 0f, 34.2f), new Vector3(3f, 2.5f, 3.4f));
            Transform runHint = FloorHint(labels, TutorialDirector.ZoneRunEnd);
            Box(runHint, "Faixa de chegada", new Vector3(0f, 0.004f, 33f), new Vector3(2.8f, 0.012f, 0.25f), _unlitYellow, _layerDefault, false);
            Label(runHint, "CORRA ATÉ AQUI", new Vector3(0f, 0.03f, 32.2f), OnFloor(Vector3.forward), 1.8f, 3f, new Color(1f, 0.85f, 0.3f));

            for (float z = 12f; z < 31f; z += 2f)
            {
                Prop(run, H + (((int)z / 2) % 2 == 0 ? "SM_Carpet_02.prefab" : "SM_Carpet_03.prefab"), new Vector3(0f, 0.002f, z), 90f, 1.6f);
            }

            for (float z = 13f; z < 33f; z += 4f)
            {
                bool left = ((int)z / 4) % 2 == 0;
                WallProp(run, H + (left ? "SM_Poster.prefab" : "SM_Poster_V2.prefab"), new Vector3(left ? -1.3f : 1.3f, 1.6f, z), left ? Vector3.right : Vector3.left);
                WallProp(run, H + "SM_Poster.prefab", new Vector3(left ? 1.3f : -1.3f, 1.55f, z + 2f), left ? Vector3.left : Vector3.right);
            }

            for (float z = 14f; z < 36f; z += 7f) Lamp(run, new Vector3(0f, 0f, z), Height);

            return spawn;
        }

        /// <summary>Sala 2 (depósito): o duto de ar a 1,3 m, a mesa a 1,08 m e a grade de 0,8 m que nem agachado passa.</summary>
        private static void BuildCrouch(Transform world, Transform labels)
        {
            Transform room = Group("2 · Agachar (depósito)", world).transform;

            Room(room, -4f, 4f, 36f, 52f, Height, FloorTiles, RoofRoom);
            Wall(room, false, -4f, 36f, 52f, Height);
            Wall(room, false, 4f, 36f, 52f, Height);
            Wall(room, true, 52f, -5f, 5f, Height, DoorGap(0f));
            Gate(room, TutorialDirector.GateDark, 0f, 52f);

            // Duto de ar atravessando a sala: fundo a 1,30 m. Em pé (1,43 m) não passa; agachado passa.
            const float ductBottom = 1.30f;
            const float ductZ = 40.84f;
            Solid(room, "Duto (colisão)", new Vector3(0f, ductBottom, ductZ), new Vector3(8f, 1.3f, 1.3f), _layerWalls);
            // Body_Big e não Body: a malha do SM_Air_Ventilation_Body tem o UV quebrado (a textura sai
            // esticada em listras). A Body_Big tem a mesma seção de 1,3 m; encurtada de 4,53 para 4 m.
            Fit(Spawn(room, W + "SM_Air_Ventilation_Body_Big.prefab", 0f, new Vector3(4f / 4.53f, 1f, 1f)), new Vector3(-2f, ductBottom, ductZ));
            Fit(Spawn(room, W + "SM_Air_Ventilation_Body_Big.prefab", 0f, new Vector3(4f / 4.53f, 1f, 1f)), new Vector3(2f, ductBottom, ductZ));
            Label(FloorHint(labels, TutorialDirector.SignCrouch), "DUTO DE AR · 1,3 m\n<size=70%>passa agachado</size>", new Vector3(0f, 1.0f, ductZ - 0.7f), FacingViewer(Vector3.back), 1.3f, 5f, Color.white);
            Zone(room, TutorialDirector.ZoneCrouchRoom, new Vector3(0f, 0f, 37.6f), new Vector3(8f, 2.5f, 2.2f));
            Zone(room, TutorialDirector.ZoneCrouchBeam, new Vector3(0f, 0f, 42.6f), new Vector3(8f, 2.5f, 1.8f));

            // Mesa: o vão de baixo tem 1,08 m. Só passa agachado e com o chapéu amassado.
            const float tableTop = 1.08f;
            const float tableZ = 46.8f;
            Solid(room, "Mesa (tampo)", new Vector3(0f, tableTop, tableZ), new Vector3(7.6f, 0.1f, 1.6f), _layerWalls);
            // Quatro mesas de 1,9 m (a do jogo tem 2 m; encolhida 5% para caber entre as paredes).
            const float tableScaleX = 0.95f;
            foreach (float x in new[] { -2.85f, -0.95f, 0.95f, 2.85f })
            {
                GameObject table = Spawn(room, W + "SM_Metal_Table_1.prefab", 0f, new Vector3(tableScaleX, (tableTop + 0.1f) / 0.85f, 2f));
                Fit(table, new Vector3(x, 0f, tableZ));
                DisableColliders(table);

                // Colisor em cada perna de verdade da mesa do jogo (mais ou menos 0,957 no X e 0,354 no Z, aqui esticado 2x).
                foreach (float legX in new[] { x - 0.957f * tableScaleX, x + 0.957f * tableScaleX })
                foreach (float legZ in new[] { tableZ - 0.708f, tableZ + 0.708f })
                {
                    Solid(room, "Mesa (perna)", new Vector3(legX, 0f, legZ), new Vector3(0.06f, tableTop, 0.06f), _layerWalls);
                }
            }

            Label(FloorHint(labels, TutorialDirector.SignCrouch), "MESA · 1 m\n<size=70%>agachado, o chapéu amassa</size>", new Vector3(0f, 1.6f, tableZ - 0.82f), FacingViewer(Vector3.back), 1.3f, 6f, Color.white);
            Zone(room, TutorialDirector.ZoneCrouchTable, new Vector3(0f, 0f, 49.6f), new Vector3(8f, 2.5f, 2.2f));

            // Grade de ventilação de 0,8 m na parede: só mostra o limite (não tem passagem).
            GameObject grate = Spawn(room, W + "SM_Air_Ventilation_Door.prefab", Face(Vector3.right, true), Vector3.one * (0.8f / 1.18f));
            Fit(grate, new Vector3(-4f + Thickness * 0.5f + 0.03f, 0.02f, 43.6f));
            Box(room, "Grade (fundo)", new Vector3(-3.81f, 0.03f, 43.6f), new Vector3(0.01f, 0.76f, 0.76f), _unlitBlack, _layerDefault, false);
            Label(FloorHint(labels, TutorialDirector.SignCrouch), "GRADE · 0,8 m\n<size=70%>nem agachado: é o limite</size>", new Vector3(-3.79f, 1.3f, 43.6f), FacingViewer(Vector3.right), 1.1f, 2.6f, new Color(1f, 0.55f, 0.45f));

            // Bastidor: cases, caixas de figurino e araras encostados nas paredes.
            Prop(room, D + "SM_Flight_Case_Big_Wheels.prefab", new Vector3(-3.2f, 0f, 37.3f), Face(Vector3.right));
            Prop(room, D + "SM_Flight_Case_Small.prefab", new Vector3(-3.3f, 0f, 38.5f), Face(Vector3.right));
            Prop(room, D + "SM_Flight_Case_Small.prefab", new Vector3(-3.3f, 0.76f, 38.5f), Face(new Vector3(1f, 0f, 0.3f)));
            Prop(room, D + "SM_Costume_Box_Metal.prefab", new Vector3(3.3f, 0f, 37.3f), Face(Vector3.left));
            Prop(room, D + "SM_Costume_Box_Open.prefab", new Vector3(3.3f, 0f, 38.4f), Face(Vector3.left));
            Prop(room, D + "SM_Clothes_Rack.prefab", new Vector3(3.4f, 0f, 50.4f), 0f);
            Prop(room, D + "SM_Cardboard_Box.prefab", new Vector3(-3.3f, 0f, 50.6f), 15f);
            Prop(room, D + "SM_Cardboard_Small_Box.prefab", new Vector3(-3.3f, 0.57f, 50.6f), -10f);
            Prop(room, D + "SM_Cardboard_Box.prefab", new Vector3(-3.2f, 0f, 49.4f), 80f);
            Prop(room, S + "SM_Pallet_V3.prefab", new Vector3(3.2f, 0f, 44f), 90f);

            Sign(labels, TutorialDirector.SignCrouch, "2 · AGACHAR", "passar por baixo", 52f, -2.7f);
            Lamp(room, new Vector3(0f, 0f, 38.3f), Height);
            Lamp(room, new Vector3(0f, 0f, 44f), Height);
            Lamp(room, new Vector3(0f, 0f, 49.8f), Height);
        }

        /// <summary>Sala 3 (almoxarifado escuro): só a lanterna. A saída é a porta com a luz, no canto.</summary>
        private static void BuildDark(Transform world, Transform labels)
        {
            Transform room = Group("3 · Lanterna (almoxarifado escuro)", world).transform;

            Room(room, -5f, 5f, 52f, 66f, Height, FloorTiles, RoofRoom);
            Wall(room, false, -5f, 52f, 66f, Height);
            Wall(room, false, 5f, 52f, 66f, Height);
            Wall(room, true, 66f, -5f, 5f, Height, DoorGap(2f));
            Gate(room, TutorialDirector.GateChat, 2f, 66f);

            Zone(room, TutorialDirector.ZoneDarkRoom, new Vector3(0f, 0f, 53.8f), new Vector3(10f, 2.5f, 2.4f));

            Prop(room, O + "ShelfConjunto.prefab", new Vector3(-4.5f, 0f, 56.5f), 0f);
            Prop(room, O + "SM_Metal_Shelf_V2.prefab", new Vector3(-4.5f, 0f, 61.5f), Face(Vector3.right));
            Prop(room, O + "SM_Iron_Locker.prefab", new Vector3(-4.4f, 0f, 64.4f), Face(Vector3.right));
            Prop(room, D + "BoxConjunto.prefab", new Vector3(-1.8f, 0f, 57.2f), 20f);
            Prop(room, S + "SM_Dolly_Cart.prefab", new Vector3(3.8f, 0f, 59.5f), 200f);
            Prop(room, S + "ConjuntoPallets.prefab", new Vector3(3.8f, 0f, 55.6f), 0f);
            Prop(room, D + "SM_Cardboard_Box.prefab", new Vector3(0.3f, 0f, 62.6f), 35f);
            Prop(room, D + "SM_Cardboard_Box.prefab", new Vector3(0.1f, 0.57f, 62.7f), 10f);
            Prop(room, D + "SM_Cardboard_Small_Box.prefab", new Vector3(-0.9f, 0f, 63.3f), 60f);
            Prop(room, S + "SM_Layher_Tower.prefab", new Vector3(-1.8f, 0f, 53.7f), 0f);
            Prop(room, O + "SM_Trash_Can_V1.prefab", new Vector3(4.4f, 0f, 64.9f), 0f);

            Sign(labels, TutorialDirector.SignFlashlight, "3 · LANTERNA", "procure a saída", 66f, -1.8f);
        }

        /// <summary>Sala 4 (camarim): o chat. O botão escondido fica na parede atrás do armário de metal.</summary>
        private static void BuildChat(Transform world, Transform labels)
        {
            Transform room = Group("4 · Chat (camarim)", world).transform;

            Room(room, -5f, 5f, 66f, 80f, Height, FloorPlanks, RoofRoom);
            Wall(room, false, -5f, 66f, 80f, Height);
            Wall(room, false, 5f, 66f, 80f, Height);
            Wall(room, true, 80f, -5f, 5f, Height, DoorGap(2f));
            Gate(room, TutorialDirector.GateItems, 2f, 80f);

            Zone(room, TutorialDirector.ZoneDarkExit, new Vector3(2f, 0f, 67.6f), new Vector3(3f, 2.5f, 2.2f));

            // Armário: dois armários de ferro de costas para a parede do fundo, com um vão de ~1 m.
            Prop(room, O + "SM_Iron_Locker.prefab", new Vector3(-4.2f, 0f, 78.6f), Face(Vector3.back));
            Prop(room, O + "SM_Iron_Locker.prefab", new Vector3(-3.0f, 0f, 78.6f), Face(Vector3.back));

            TutorialButton button = Button(room, new Vector3(-3.7f, 1.25f, 80f - Thickness * 0.5f - 0.02f), Vector3.back);
            Refs["hiddenButton"] = button;

            // Camarim: araras, cases e caixas de figurino, sofá e biombo.
            Prop(room, D + "ConjuntoFlightCases.prefab", new Vector3(3.9f, 0f, 70.4f), Face(Vector3.left));
            Prop(room, D + "SM_Clothes_Rack.prefab", new Vector3(4.2f, 0f, 73f), 0f);
            Prop(room, D + "SM_Flight_Case_Big.prefab", new Vector3(4.3f, 0f, 75.4f), Face(Vector3.left));
            Prop(room, D + "SM_Top_Hat.prefab", new Vector3(4.3f, 0.76f, 75.4f), 20f);
            Prop(room, D + "SM_High_Stool.prefab", new Vector3(3.2f, 0f, 74.4f), 30f);
            Prop(room, D + "SM_Costume_Box_Metal.prefab", new Vector3(4.3f, 0f, 68.6f), Face(Vector3.left));
            Prop(room, B + "SM_Sofa.prefab", new Vector3(-4.3f, 0f, 71.5f), Face(Vector3.right));
            Prop(room, D + "SM_Clothes_Rack.prefab", new Vector3(-1.2f, 0f, 77.6f), 90f);
            Prop(room, D + "SM_Costume_Box_Open.prefab", new Vector3(0.6f, 0f, 78.7f), Face(Vector3.back));
            Prop(room, D + "SM_Costume_Box.prefab", new Vector3(-4.4f, 0f, 67.4f), Face(Vector3.right));
            WallProp(room, H + "SM_Poster_V2.prefab", new Vector3(3.9f, 1.7f, 66.2f), Vector3.forward);
            Prop(room, D + "SM_Biombo.prefab", new Vector3(-3.1f, 0f, 68.3f), Face(new Vector3(1f, 0f, 1f)));
            Prop(room, D + "SM_Costume_Box_Metal.prefab", new Vector3(4.4f, 0f, 77.9f), Face(Vector3.left));
            Prop(room, D + "SM_Costume_Box_Open_Metal.prefab", new Vector3(4.4f, 0f, 78.9f), Face(Vector3.left));
            Prop(room, D + "SM_Flight_Case_Small_Wheels.prefab", new Vector3(-1.2f, 0f, 70.2f), 25f);

            Sign(labels, TutorialDirector.SignChat, "4 · CHAT", "o chat te ajuda", 80f, -0.6f);
            Lamp(room, new Vector3(0f, 0f, 70f), Height);
            Lamp(room, new Vector3(0f, 0f, 76f), Height);
        }

        /// <summary>Sala 5 (almoxarifado): a pilha na mesa e a caixa que não é sua.</summary>
        private static void BuildItems(Transform world, Transform labels)
        {
            Transform room = Group("5 · Itens (almoxarifado)", world).transform;

            Room(room, -5f, 5f, 80f, 92f, Height, FloorTiles, RoofRoom);
            Wall(room, false, -5f, 80f, 92f, Height);
            Wall(room, false, 5f, 80f, 92f, Height);
            Wall(room, true, 92f, -5f, 5f, Height, DoorGap(0f));
            Gate(room, TutorialDirector.GateDoors, 0f, 92f);
            Zone(room, TutorialDirector.ZoneItemsRoom, new Vector3(0f, 0f, 81.3f), new Vector3(10f, 2.5f, 2.4f));

            const float tableTop = 0.85f;
            Prop(room, W + "SM_Metal_Table_1.prefab", new Vector3(0f, 0f, 86.5f), 0f);

            GameObject battery = PlaceOnFloor(room, Prefabs + "Items/Pickables/FlashlightBatteryPickable.prefab", new Vector3(0f, tableTop, 86.5f), 90f);
            Refs["battery"] = battery != null ? battery.GetComponent<ItemPickable>() : null;

            // Caixa de missão sem puzzle ligado: o filtro de missão recusa todo mundo (INDISPONÍVEL).
            const float caseTop = 0.76f;
            Prop(room, D + "SM_Flight_Case_Big.prefab", new Vector3(2.7f, 0f, 86.5f), 90f);
            GameObject box = PlaceOnFloor(room, Prefabs + "Items/Pickables/Missions/Box/BoxSquarePickable.prefab", new Vector3(2.7f, caseTop, 86.5f), 0f);
            Refs["blockedBox"] = box;

            // Sem o "fantasma" de PEGAR ao chegar perto: a caixa só mostra o aviso quando a mira para
            // nela (INDISPONÍVEL). Alcance quase zero só corta a revelação de longe.
            if (box != null)
            {
                foreach (InteractionMarker boxMarker in box.GetComponentsInChildren<InteractionMarker>(true))
                {
                    SerializedObject so = new SerializedObject(boxMarker);
                    so.FindProperty("revealDistance").floatValue = 0.01f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Esta estante é o ACHADOS E PERDIDOS (na partida, quem morre deixa os itens numa assim).
            Prop(room, O + "SM_Metal_Shelf_V2.prefab", new Vector3(-4.5f, 0f, 83.5f), Face(Vector3.right));
            Refs["lostAndFound"] = LostAndFoundSign(room, new Vector3(-4.79f, 2.25f, 83.5f), 90f);
            Prop(room, O + "SM_Metal_Shelf_V1.prefab", new Vector3(-4.5f, 0f, 88.5f), Face(Vector3.right));
            Prop(room, O + "SM_Metal_Shelf_V2.prefab", new Vector3(4.5f, 0f, 89.6f), Face(Vector3.left));
            Prop(room, O + "SM_Trash_Can_V1.prefab", new Vector3(4.4f, 0f, 81.6f), 0f);
            Prop(room, W + "SM_Floor_Sign.prefab", new Vector3(-1.6f, 0f, 82.4f), 200f);
            Prop(room, D + "SM_Cardboard_Box.prefab", new Vector3(4.3f, 0f, 84.2f), 80f);
            Prop(room, O + "SM_Iron_Locker.prefab", new Vector3(4.45f, 0f, 86.6f), Face(Vector3.left));
            Prop(room, O + "SM_Iron_Locker.prefab", new Vector3(3.6f, 0f, 91.4f), Face(Vector3.back));
            Prop(room, S + "SM_Pallet_V2.prefab", new Vector3(-3.5f, 0f, 81.2f), 10f);
            Prop(room, D + "SM_Cardboard_Small_Box.prefab", new Vector3(-3.5f, 0.15f, 81.2f), 40f);

            Label(FloorHint(labels, TutorialDirector.FloorHintBattery), "PILHA", new Vector3(0f, 0.03f, 85.6f), OnFloor(Vector3.forward), 1.6f, 2f, new Color(1f, 0.85f, 0.3f));
            Sign(labels, TutorialDirector.SignItems, "5 · ITENS E BATERIA", "pegar · segurar · usar", 92f, -2.8f);
            Lamp(room, new Vector3(0f, 0f, 84.5f), Height);
            Lamp(room, new Vector3(0f, 0f, 89.5f), Height);
        }

        /// <summary>Sala 6 (corredor de portas): duas portas na parede do fundo. A da esquerda está presa pelo "monstro".</summary>
        private static void BuildDoors(Transform world, Transform labels)
        {
            Transform room = Group("6 · Portas", world).transform;

            Room(room, -5f, 5f, 92f, 102f, Height, FloorHall, RoofHall, true);
            Zone(room, TutorialDirector.ZoneDoorsRoom, new Vector3(0f, 0f, 93.4f), new Vector3(10f, 2.5f, 2.2f));
            Wall(room, false, -5f, 92f, 102f, Height);
            Wall(room, false, 5f, 92f, 102f, Height);
            Wall(room, true, 102f, -6f, 6f, Height, DoorGap(-2.5f), DoorGap(2.5f));

            Door left = DoorAt(room, Doors + "Door_Black.prefab", -2.5f, 102f, true);
            Door right = DoorAt(room, Doors + "Door_Black.prefab", 2.5f, 102f, true);
            Refs["lockedDoor"] = left;
            Refs["openDoor"] = right;

            float face = 102f - Thickness * 0.5f - 0.02f;
            Label(FloorHint(labels, TutorialDirector.SignDoors), "TRAVADA?", new Vector3(-2.5f, 2.5f, face), FacingViewer(Vector3.back), 1.2f, 2.5f, new Color(1f, 0.55f, 0.45f));
            Label(FloorHint(labels, TutorialDirector.SignDoors), "ABERTA", new Vector3(2.5f, 2.5f, face), FacingViewer(Vector3.back), 1.2f, 2.5f, new Color(0.5f, 1f, 0.6f));
            Label(FloorHint(labels, TutorialDirector.SignDoors), "<b>6 · PORTAS</b>", new Vector3(0f, 2.0f, face), FacingViewer(Vector3.back), 2f, 3f, new Color(1f, 0.85f, 0.35f));

            Prop(room, H + "SM_Big_Bench.prefab", new Vector3(-4.4f, 0f, 96.5f), 0f);
            Prop(room, H + "SM_Small_Bench.prefab", new Vector3(4.4f, 0f, 95.5f), 0f);
            WallProp(room, H + "SM_Poster.prefab", new Vector3(-4.8f, 1.65f, 99.4f), Vector3.right);
            Prop(room, H + "SM_Carpet_05.prefab", new Vector3(0f, 0.002f, 96.5f), 0f, 2f);

            Lamp(room, new Vector3(0f, 0f, 96f), Height);
            Lamp(room, new Vector3(0f, 0f, 100.2f), Height);
        }

        /// <summary>Sala 7 (exposição): três objetos de audiência em cima de cases, com corda e refletor.</summary>
        private static void BuildAudience(Transform world, Transform labels)
        {
            Transform room = Group("7 · Audiência (exposição)", world).transform;

            Room(room, -6f, 6f, 102f, 118f, Height, FloorMarble, RoofFoyer, true);
            Wall(room, false, -6f, 102f, 118f, Height);
            Wall(room, false, 6f, 102f, 118f, Height);
            Wall(room, true, 118f, -6f, 6f, Height, DoorGap(0f));
            Gate(room, TutorialDirector.GatePuzzle, 0f, 118f);

            Zone(room, TutorialDirector.ZoneDoorsAfter, new Vector3(3f, 0f, 104.4f), new Vector3(6f, 2.5f, 4.4f));

            string[] props =
            {
                "Audience_Objects/SM_Crown.prefab",
                "Audience_Objects/Sword.prefab",
                "Audience_Objects/Sad_Mask_Low.prefab",
            };

            float[] xs = { -3.5f, 0f, 3.5f };
            List<GameObject> placed = new();
            const float pedestal = 0.76f;
            const float exhibitZ = 112.5f;

            for (int i = 0; i < props.Length; i++)
            {
                Prop(room, D + "SM_Flight_Case_Big.prefab", new Vector3(xs[i], 0f, exhibitZ), 0f);
                GameObject prop = PlaceOnFloor(room, Prefabs + props[i], new Vector3(xs[i], pedestal, exhibitZ), 180f);
                if (prop != null) placed.Add(prop);

                Queue(room, new Vector3(xs[i] - 0.6f, 0f, exhibitZ - 1.1f), new Vector3(xs[i] + 0.6f, 0f, exhibitZ - 1.1f));
            }

            _audienceProps = placed.ToArray();

            Prop(room, S + "SM_Tripod_Spotilight_V1.prefab", new Vector3(-5.2f, 0f, 109f), Face(new Vector3(1.4f, 0f, 3.5f)));
            Prop(room, S + "SM_Tripod_Spotilight_V2.prefab", new Vector3(5.2f, 0f, 109f), Face(new Vector3(-1.4f, 0f, 3.5f)));
            Prop(room, H + "SM_Small_Bench.prefab", new Vector3(-3f, 0f, 107f), 90f);
            Prop(room, H + "SM_Big_Bench.prefab", new Vector3(-5.4f, 0f, 112.5f), 0f);
            Prop(room, H + "SM_Big_Bench.prefab", new Vector3(5.4f, 0f, 112.5f), 0f);
            Prop(room, H + "SM_Carpet_04.prefab", new Vector3(0f, 0.002f, 109.5f), 0f, 2.4f);
            Prop(room, H + "SM_Carpet_04.prefab", new Vector3(-3.5f, 0.002f, 109.5f), 0f, 2.4f);
            Prop(room, H + "SM_Carpet_04.prefab", new Vector3(3.5f, 0.002f, 109.5f), 0f, 2.4f);
            WallProp(room, H + "SM_Poster.prefab", new Vector3(-5.8f, 1.65f, 106f), Vector3.right);
            WallProp(room, H + "SM_Poster_V2.prefab", new Vector3(-5.8f, 1.65f, 110f), Vector3.right);
            WallProp(room, H + "SM_Poster.prefab", new Vector3(5.8f, 1.65f, 110f), Vector3.left);
            WallProp(room, H + "SM_Poster_V2.prefab", new Vector3(5.8f, 1.6f, 114f), Vector3.left);

            Sign(labels, TutorialDirector.SignAudience, "7 · AUDIÊNCIA", "grave o que tem olho", 118f, -2.9f);
            Lamp(room, new Vector3(-3f, 0f, 106.5f), Height);
            Lamp(room, new Vector3(3f, 0f, 106.5f), Height);
            Lamp(room, new Vector3(0f, 0f, 111f), Height);
            Lamp(room, new Vector3(0f, 0f, 115.5f), Height);
        }

        /// <summary>
        /// Sala 8 (oficina): o puzzle dos canos de verdade (PipesManager), encaixado na parede da direita
        /// do jeito que ele fica na sala de missão de canos do jogo, com os canos de entrada e saída, mesas
        /// com pedaços de cano e prateleiras.
        /// </summary>
        private static void BuildPuzzle(Transform world, Transform labels)
        {
            Transform room = Group("8 · Puzzle (oficina de canos)", world).transform;

            Room(room, -6f, 6f, 118f, 134f, Height, FloorTiles, RoofRoom);
            Wall(room, false, -6f, 118f, 134f, Height);
            Wall(room, false, 6f, 118f, 134f, Height);
            Wall(room, true, 134f, -9f, 9f, StageHeight, DoorGap(0f));
            Gate(room, TutorialDirector.GateStage, 0f, 134f);

            Zone(room, TutorialDirector.ZonePuzzleRoom, new Vector3(0f, 0f, 120f), new Vector3(12f, 2.5f, 2.6f));

            // Mesma pose da sala de canos do jogo (RoomData MissionPipesRoom): parede em +X, yaw 180.
            Vector3 managerPosition = new Vector3(6f - Thickness * 0.5f - 0.07f, 0.576f, 126f);
            GameObject manager = PlaceAt(room, Prefabs + "Missions/Totems/Pipes/PipesManager.prefab", managerPosition, 180f);

            if (manager != null)
            {
                // Só o layout PipeGrid: ele encaixa nos canos de entrada e saída da parede. O PipeGrid2
                // (o outro sorteio do prefab) não está encaixado e fica com folgas e por cima dos canos.
                SerializedObject layouts = new SerializedObject(manager.GetComponent<PipesPuzzleManager>());
                SerializedProperty list = layouts.FindProperty("possibleGridLayouts");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<PipeGridLayout>("Assets/Scriptable_objects/Missions/PipeGrid.asset");
                layouts.ApplyModifiedPropertiesWithoutUndo();

                Refs["puzzle"] = manager.GetComponent<PipesPuzzleManager>();
                Refs["puzzleSelector"] = manager.GetComponent<MissionOwnershipSelector>();
                CopyPipeWall(room, manager.transform);
            }

            // Bancada no meio com pedaços de cano: mostra a peça que gira na parede.
            const float top = 0.85f;
            Prop(room, W + "SM_Metal_Table_1.prefab", new Vector3(-1.6f, 0f, 125.4f), 0f);
            Prop(room, W + "SM_Metal_Table_1.prefab", new Vector3(-1.6f, 0f, 126.2f), 0f);
            PipeProp(room, PipeModels + "SM_Pipe_L.fbx", new Vector3(-2.2f, top, 125.5f), 30f);
            PipeProp(room, PipeModels + "SM_Pipe_Small.fbx", new Vector3(-1.5f, top, 125.3f), 80f);
            PipeProp(room, PipeModels + "SM_Pipe_L.fbx", new Vector3(-0.9f, top, 126.3f), 200f);
            PipeProp(room, PipeModels + "SM_Pipe_Small.fbx", new Vector3(-2.0f, top, 126.4f), 10f);
            Prop(room, B + "SM_Chair_Foyer.prefab", new Vector3(-1.2f, 0f, 124.4f), Face(Vector3.forward));
            Prop(room, B + "SM_Chair_Foyer.prefab", new Vector3(-2.2f, 0f, 127.3f), Face(Vector3.back));

            Prop(room, O + "ShelfConjunto.prefab", new Vector3(-5.5f, 0f, 122.5f), 0f);
            Prop(room, O + "SM_Metal_Shelf_V2.prefab", new Vector3(-5.5f, 0f, 128.6f), Face(Vector3.right));
            Prop(room, O + "SM_Metal_Shelf_V1.prefab", new Vector3(-5.5f, 0f, 130.6f), Face(Vector3.right));
            Prop(room, O + "SM_Iron_Locker.prefab", new Vector3(-4.6f, 0f, 133.4f), Face(Vector3.back));
            Prop(room, O + "SM_Iron_Locker.prefab", new Vector3(-3.4f, 0f, 133.4f), Face(Vector3.back));
            Prop(room, S + "ConjuntoPallets.prefab", new Vector3(-4.6f, 0f, 119.4f), 0f);
            Prop(room, W + "SM_Bathroom_Trash.prefab", new Vector3(3.6f, 0f, 133.2f), 0f);
            Prop(room, O + "SM_Trash_Can_V1.prefab", new Vector3(1.6f, 0f, 133.4f), 0f);

            // Só uma folha no chão (as outras quatro foram tiradas à mão na cena, 2026-11-24).
            float[,] papers = { { -3.2f, 124.3f, 15f } };
            for (int i = 0; i < papers.GetLength(0); i++)
            {
                Prop(room, H + "SM_Paper_28x40_V2.prefab", new Vector3(papers[i, 0], 0.003f, papers[i, 1]), papers[i, 2]);
            }


            // Do lado direito da porta: no esquerdo ficam os armários de ferro.
            Sign(labels, TutorialDirector.SignPuzzle, "8 · MISSÃO E PUZZLE", "leia · interprete · resolva", 134f, 2.9f);
            Lamp(room, new Vector3(-3f, 0f, 122f), Height);
            Lamp(room, new Vector3(3f, 0f, 122f), Height);
            Lamp(room, new Vector3(-3f, 0f, 129.5f), Height);
            Lamp(room, new Vector3(3f, 0f, 129.5f), Height);
        }

        /// <summary>
        /// Os canos de entrada e saída da sala de canos do jogo, na mesma posição em relação ao
        /// PipesManager: é o que dá sentido ao puzzle (a água vem de um lado e precisa chegar no outro).
        /// </summary>
        private static void CopyPipeWall(Transform parent, Transform manager)
        {
            GameObject roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Rooms/Missions/MissionRoomPipes.prefab");
            if (roomPrefab == null) return;

            // Pose do PipesManager na sala do jogo (RoomData MissionPipesRoom).
            Vector3 gameManagerPosition = new Vector3(5.762f, 0.576f, -1.127f);
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab);

            try
            {
                Transform group = Group("Canos da parede", parent).transform;
                Vector3 delta = manager.position - gameManagerPosition;

                foreach (Transform piece in temp.GetComponentsInChildren<Transform>(true))
                {
                    if (!piece.name.StartsWith("SM_Pipe_")) continue;
                    if (piece.position.x < 5.5f) continue;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(piece.gameObject)) continue;

                    // O modelo do cano em si (o .fbx), não o prefab de sala que contém ele.
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(piece.gameObject);
                    if (source == null || !AssetDatabase.GetAssetPath(source).EndsWith(".fbx")) continue;

                    GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(source, group);
                    copy.transform.SetPositionAndRotation(piece.position + delta, piece.rotation);
                    copy.transform.localScale = piece.lossyScale;

                    // A sala do jogo troca o material do modelo (M_Pipes); o .fbx sozinho vem com o padrão.
                    Renderer from = piece.GetComponentInChildren<Renderer>(true);
                    Renderer to = copy.GetComponentInChildren<Renderer>(true);
                    if (from != null && to != null) to.sharedMaterials = from.sharedMaterials;
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>Sala 9 (auditório): o palco do jogo com o monstro congelado, a plateia, o MainStage e o quadro de luz.</summary>
        private static Transform BuildStage(Transform world, Transform labels)
        {
            Transform room = Group("9 · Palco (auditório)", world).transform;

            Room(room, -9f, 9f, 134f, 160f, StageHeight, FloorHall, RoofFoyer);
            Wall(room, false, -9f, 134f, 160f, StageHeight);
            Wall(room, false, 9f, 134f, 160f, StageHeight);
            Wall(room, true, 160f, -9f, 9f, StageHeight, DoorGap(7f));
            Gate(room, TutorialDirector.GateFuga, 7f, 160f);

            Zone(room, TutorialDirector.ZoneStageRoom, new Vector3(0f, 0f, 135.8f), new Vector3(18f, 2.5f, 2.4f));

            // Palco do jogo, menor (60%), apoiado no piso (0,9 m). No modelo a frente (curva, com as
            // escadas nos cantos) fica para +Z, como na Game, onde a plateia está em +Z; aqui a plateia
            // está em -Z, então ele gira 180°. A colisão é a própria malha (frente curva e degraus).
            const float stageTop = 0.9f;
            const float stageFront = 150f;
            GameObject stage = Spawn(room, S + "SM_Stage.prefab", 180f, Vector3.one * 0.6f);
            Bounds stageBounds = RendererBounds(stage);
            stage.transform.position += new Vector3(-stageBounds.center.x, stageTop - stageBounds.max.y, stageFront - stageBounds.min.z);

            foreach (MeshFilter stageMesh in stage.GetComponentsInChildren<MeshFilter>(true))
            {
                if (stageMesh.sharedMesh == null || stageMesh.GetComponent<Collider>() != null) continue;

                stageMesh.gameObject.layer = _layerGround;
                stageMesh.gameObject.AddComponent<MeshCollider>().sharedMesh = stageMesh.sharedMesh;
                GameObjectUtility.SetStaticEditorFlags(stageMesh.gameObject, StaticEditorFlags.NavigationStatic);
            }

            StageSides(room, RendererBounds(stage), stageTop);

            foreach (float x in new[] { -4f, 0f, 4f })
            {
                Fit(Spawn(room, S + "SM_Stage_Wall_V2.prefab", 0f), new Vector3(x, 0f, 159.55f));
            }

            GameObject curtainLeft = Spawn(room, S + "SM_Curtain_Left.prefab", 0f, Vector3.one * 0.4f);
            Fit(curtainLeft, new Vector3(-4.6f, stageTop, 158.8f));
            GameObject curtainRight = Spawn(room, S + "SM_Curtain_Right.prefab", 0f, Vector3.one * 0.4f);
            Fit(curtainRight, new Vector3(4.6f, stageTop, 158.8f));

            // As caixas grandes ficam nas marcas de fita do fundo do palco (a frente é curva e tem as
            // escadas nos cantos: lá elas ficariam no ar ou na frente dos degraus).
            Prop(room, S + "SM_SoundBox.prefab", new Vector3(-5.4f, stageTop, 155.6f), Face(Vector3.back));
            Prop(room, S + "SM_SoundBox.prefab", new Vector3(5.4f, stageTop, 155.6f), Face(Vector3.back));
            Prop(room, S + "SM_SoundBox_Floor.prefab", new Vector3(-2.2f, stageTop, 150.9f), Face(Vector3.back));
            Prop(room, S + "SM_SoundBox_Floor.prefab", new Vector3(2.2f, stageTop, 150.9f), Face(Vector3.back));
            Prop(room, S + "SM_Microphone.prefab", new Vector3(-1.4f, stageTop, 152f), 0f);

            Prop(room, S + "SM_Tripod_Spotilight_V1.prefab", new Vector3(-7.6f, 0f, 148.6f), Face(new Vector3(0.7f, 0f, 1f)));
            Prop(room, S + "SM_Tripod_Spotilight_V2.prefab", new Vector3(8.2f, 0f, 149.2f), Face(new Vector3(-0.7f, 0f, 1f)));
            Prop(room, S + "SM_Tripod_SoundBox.prefab", new Vector3(-7.9f, 0f, 147.6f), Face(Vector3.right));

            // Plateia: três fileiras de cada lado, com o corredor do meio livre até a marca.
            foreach (float z in new[] { 138.8f, 140.3f, 141.8f })
            {
                for (int i = 0; i < 7; i++)
                {
                    float offset = 1.8f + i * 0.74f;
                    Prop(room, A + "SM_Chair.prefab", new Vector3(-offset, 0f, z), 0f);
                    Prop(room, A + "SM_Chair.prefab", new Vector3(offset, 0f, z), 0f);
                }
            }

            Transform home = Marker(room, "Monstro (casa)", new Vector3(0f, stageTop, 154.5f), Vector3.back);

            // O sensor de visão do jogador só vê o que está numa faixa de 1,3 m em volta da mira. O
            // gatilho do palco fica na altura do corpo do monstro: quem enquadra o monstro pega os dois.
            GameObject mainStage = PlaceAt(room, Prefabs + "Audience_Objects/MainStage.prefab", new Vector3(0f, 2.0f, 153.4f), 0f);
            if (mainStage != null) mainStage.name = "MainStage";

            // "Procure marcas no chão": o X de onde gravar, a uns 7 m do monstro.
            Transform recordHint = FloorHint(labels, TutorialDirector.FloorHintRecordMark);
            Box(recordHint, "Marca (fita 1)", new Vector3(0f, 0.004f, 147.2f), new Vector3(1.6f, 0.012f, 0.18f), _unlitYellow, _layerDefault, false)
                .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Box(recordHint, "Marca (fita 2)", new Vector3(0f, 0.004f, 147.2f), new Vector3(1.6f, 0.012f, 0.18f), _unlitYellow, _layerDefault, false)
                .transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            Label(recordHint, "MARCA DE GRAVAÇÃO", new Vector3(0f, 0.03f, 145.9f), OnFloor(Vector3.forward), 1.6f, 4f, new Color(1f, 0.85f, 0.3f));

            Label(labels, "PALCO PRINCIPAL", new Vector3(0f, 4.3f, 159.25f), FacingViewer(Vector3.back), 3f, 8f, new Color(1f, 0.85f, 0.35f));

            // Quadro de luz na parede esquerda, de frente para a sala.
            GameObject circuit = PlaceAt(room, Prefabs + "Items/EletricCircuit.prefab", new Vector3(-9f + Thickness * 0.5f + 0.05f, 1.2f, 144f), 90f);
            Refs["circuit"] = circuit != null ? circuit.GetComponent<ElectricCircuit>() : null;
            SetMarkerLabel(circuit, "Religar");
            Label(FloorHint(labels, TutorialDirector.SignLights), "QUADRO DE LUZ", new Vector3(-8.78f, 2.4f, 144f), FacingViewer(Vector3.right), 1.3f, 3f, Color.white);

            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -4.5f : 4.5f;
                float z = i < 2 ? 139.5f : 146.5f;
                Lamp(room, new Vector3(x, 0f, z), StageHeight);
            }

            Lamp(room, new Vector3(-3f, 0f, 154f), StageHeight);
            Lamp(room, new Vector3(3f, 0f, 154f), StageHeight);
            Lamp(room, new Vector3(0f, 0f, 151f), StageHeight);
            Lamp(room, new Vector3(0f, 0f, 157.5f), StageHeight);

            EmergencyLamp(room, new Vector3(-8.5f, 3.8f, 140f));
            EmergencyLamp(room, new Vector3(8.5f, 3.8f, 140f));
            EmergencyLamp(room, new Vector3(-8.5f, 3.8f, 152f));
            EmergencyLamp(room, new Vector3(8.5f, 3.8f, 152f));

            Refs["monsterRelease"] = Marker(room, "Monstro solto aqui", new Vector3(7.4f, 0f, 157f), Vector3.forward);

            Sector(room, "Setor palco", new Vector3(0f, 0f, 146f), 8f);

            Label(FloorHint(labels, TutorialDirector.SignRecording), "<b>9 · GRAVAÇÃO</b>", new Vector3(-3f, 2.2f, 134f + Thickness * 0.5f + 0.02f), FacingViewer(Vector3.forward), 2f, 5f, new Color(1f, 0.85f, 0.35f));

            return home;
        }

        /// <summary>A fuga: corredor em S com duas portas até o saguão da saída.</summary>
        private static void BuildEscape(Transform world, Transform labels)
        {
            Transform root = Group("10 · Fuga", world).transform;

            // Trecho A: x 5,5..8,5, z 160..178, abre para o leste no fim.
            Room(root, 5.5f, 8.5f, 160f, 178f, Height, FloorHall, RoofHall, true);
            Wall(root, false, 5.5f, 160f, 178f, Height);
            Wall(root, false, 8.5f, 160f, 175f, Height);
            Wall(root, true, 178f, 5.5f, 8.5f, Height);

            // Trecho B: x 8,5..28,5, z 175..178.
            Room(root, 8.5f, 28.5f, 175f, 178f, Height, FloorHall, RoofHall, true);
            Wall(root, true, 175f, 8.5f, 28.5f, Height);
            Wall(root, true, 178f, 8.5f, 25.5f, Height);
            Wall(root, false, 28.5f, 175f, 178f, Height);

            Wall(root, false, 17f, 175f, 178f, Height, DoorGap(176.5f));
            EscapeDoors.Add(DoorAt(root, Doors + "Door_Red.prefab", 17f, 176.5f, false));

            // Trecho C: x 25,5..28,5, z 178..196.
            Room(root, 25.5f, 28.5f, 178f, 196f, Height, FloorHall, RoofHall, true);
            Wall(root, false, 25.5f, 178f, 196f, Height);
            Wall(root, false, 28.5f, 178f, 196f, Height);

            Wall(root, true, 187f, 25.5f, 28.5f, Height, DoorGap(27f));
            EscapeDoors.Add(DoorAt(root, Doors + "Door_Red.prefab", 27f, 187f, true));

            // Saguão da saída, com a porta dupla de emergência (de enfeite, trancada).
            Room(root, 23f, 33f, 196f, 204f, Height, FloorMarble, RoofFoyer, true);
            Wall(root, true, 196f, 23f, 33f, Height, Passage(27f, 3f));
            Wall(root, true, 204f, 23f, 33f, Height, DoubleDoorFrame(28f));
            Wall(root, false, 23f, 196f, 204f, Height);
            Wall(root, false, 33f, 196f, 204f, Height);
            DecorDoors.Add(DoorAt(root, Doors + "Door_Double_Exit.prefab", 28f, 204f, true));
            ExitLight(root, new Vector3(28f, 2.45f, 204f - Thickness * 0.5f - 0.03f), Vector3.back, _exitLamp, new Color(0.1f, 1f, 0.25f));

            Prop(root, B + "SM_Sofa.prefab", new Vector3(24f, 0f, 200f), Face(Vector3.right));
            Prop(root, B + "SM_Popcorn_Machine.prefab", new Vector3(32.4f, 0f, 199f), Face(Vector3.left));
            Queue(root, new Vector3(26.3f, 0f, 200.3f), new Vector3(26.3f, 0f, 202.9f));
            Queue(root, new Vector3(29.7f, 0f, 200.3f), new Vector3(29.7f, 0f, 202.9f));

            // Placas de saída pelo caminho.
            ExitLight(root, new Vector3(7f, 2.45f, 178f - Thickness * 0.5f - 0.03f), Vector3.back, _exitLamp, new Color(0.1f, 1f, 0.25f));
            ExitLight(root, new Vector3(28.5f - Thickness * 0.5f - 0.03f, 2.45f, 176.5f), Vector3.left, _exitLamp, new Color(0.1f, 1f, 0.25f));
            ExitLight(root, new Vector3(27f, 2.45f, 196f + 0.05f), Vector3.back, _exitLamp, new Color(0.1f, 1f, 0.25f));

            for (float z = 163f; z < 176f; z += 4f) WallProp(root, H + "SM_Poster.prefab", new Vector3(5.7f, 1.6f, z), Vector3.right);
            for (float x = 11f; x < 25f; x += 5f) WallProp(root, H + "SM_Poster.prefab", new Vector3(x, 1.55f, 175.2f), Vector3.forward);
            for (float z = 181f; z < 195f; z += 5f) WallProp(root, H + "SM_Poster_V2.prefab", new Vector3(28.3f, 1.6f, z), Vector3.left);

            Zone(root, TutorialDirector.ZoneFugaStart, new Vector3(7f, 0f, 162f), new Vector3(3f, 2.5f, 2f));
            Zone(root, TutorialDirector.ZoneExit, new Vector3(28f, 0f, 200.5f), new Vector3(10f, 2.5f, 6.5f));
            Refs["fugaRetry"] = Marker(root, "Recomeço da fuga", new Vector3(7f, 0.05f, 163f), Vector3.forward);

            Label(labels, "SAÍDA >>", new Vector3(7f, 2.0f, 177.8f), FacingViewer(Vector3.back), 2f, 3f, new Color(0.4f, 1f, 0.5f));
            Label(labels, "<< SAÍDA", new Vector3(28.3f, 2.0f, 176.5f), FacingViewer(Vector3.left), 2f, 3f, new Color(0.4f, 1f, 0.5f));
            Label(labels, "<b>SAÍDA</b>", new Vector3(28f, 2.75f, 203.8f), FacingViewer(Vector3.back), 3f, 6f, new Color(0.4f, 1f, 0.5f));
            Box(FloorHint(labels, TutorialDirector.ZoneExit), "Faixa verde", new Vector3(28f, 0.004f, 199f), new Vector3(6f, 0.012f, 0.3f), _unlitGreen, _layerDefault, false);

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

            // Managers do jogo, os mesmos prefabs da partida.
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

            // Sem o comentário "o monstro está segurando a porta": as portas de lição trancadas não são
            // o monstro, e o diretor explica elas no chat.
            GameObject hub = PlaceAt(systems, Prefabs + "Managers/ChatEventHub.prefab", Vector3.zero, 0f);
            SerializedObject hubSo = new SerializedObject(hub.GetComponent<ChatEventHub>());
            hubSo.FindProperty("missionNudge.idleSeconds").floatValue = 100000f;
            hubSo.FindProperty("explorationNudge.idleSeconds").floatValue = 100000f;
            SerializedProperty doorRange = hubSo.FindProperty("doorCommentRange");
            if (doorRange != null) doorRange.floatValue = 0f;
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

            // Visual do HUD do jogo: painel e etiqueta de MISSÕES, papel do Opções, fita e botão da vitória.
            Set(hud, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Acme-Regular SDF.asset"));
            Set(hud, "titleFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/FreckleFace-Regular SDF.asset"));
            Set(hud, "buttonFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Lalezar-Regular SDF.asset"));
            Set(hud, "panelSprite", HudSprite("HUD/UI_BackgroundMissions"));
            Set(hud, "labelSprite", HudSprite("HUD/UI_BackgroundTitleMissions"));
            Set(hud, "lineSprite", HudSprite("HUD/UI_LineMissions"));
            Set(hud, "cardSprite", HudSprite("Settings/UI_BackgroundSettings"));
            Set(hud, "filmSprite", HudSprite("Loading/Film_Straight"));
            Set(hud, "buttonSprite", HudSprite("Menu/Button_ON"));
            Set(hud, "decorationSprite", HudSprite("Settings/UI_Line_Decoration"));

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
            dir.FindProperty("lostAndFound").objectReferenceValue = Refs.GetValueOrDefault("lostAndFound");

            FillArray(dir.FindProperty("audienceProps"), _audienceProps);
            FillArray(dir.FindProperty("escapeDoors"), EscapeDoors.Where(d => d != null).ToArray());
            FillArray(dir.FindProperty("decorDoors"), DecorDoors.Where(d => d != null).ToArray());

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

            // NavMesh pelos colisores: paredes e piso são as caixas invisíveis do gerador (as peças de
            // parede do jogo não têm colisor). Portas (camada Doors) ficam de fora, como no jogo.
            GameObject navMeshGo = new GameObject("NavMesh");
            navMeshGo.transform.SetParent(systems, false);
            NavMeshSurface surface = navMeshGo.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default", "Ground", "Interactable", "Scenary", "Props", "MainStage", "Recordable", "Walls");
        }

        private static void FillArray(SerializedProperty property, Object[] values)
        {
            if (property == null) return;

            property.arraySize = values?.Length ?? 0;
            for (int i = 0; i < property.arraySize; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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

            // Vazio de propósito: a missão do tutorial é entregue pelo diretor.
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

        // ================================================================== paredes, pisos e portas

        private readonly struct Opening
        {
            public readonly float Center;
            public readonly float Width;
            public readonly float Height;
            public readonly string Piece;
            public readonly float PieceLength;
            public readonly bool Blocked;

            public Opening(float center, float width, float height, string piece, float pieceLength, bool blocked)
            {
                Center = center;
                Width = width;
                Height = height;
                Piece = piece;
                PieceLength = pieceLength;
                Blocked = blocked;
            }
        }

        /// <summary>Vão de porta simples: peça de parede com batente do jogo (2 m), passagem da largura da porta.</summary>
        private static Opening DoorGap(float center) =>
            new Opening(center, DoorWidth + 0.02f, DoorLintel, H + "SM_Sophisticated_Wall_Single_Door_2m_V1.prefab", 2f, false);

        /// <summary>Batente de porta dupla (4 m) sem passagem: a porta de emergência do fim é enfeite.</summary>
        private static Opening DoubleDoorFrame(float center) =>
            new Opening(center, 0f, 0f, H + "SM_Sophisticated_Wall_Double_Door_4m_V1.prefab", 4f, true);

        /// <summary>Passagem aberta, sem porta.</summary>
        private static Opening Passage(float center, float width) => new Opening(center, width, 99f, null, width, false);

        /// <summary>Piso (colisor + ladrilhos do jogo) e teto (peças do jogo) de uma sala retangular.</summary>
        private static void Room(Transform parent, float x0, float x1, float z0, float z1, float height, string floor, string roof, bool wainscot = false)
        {
            // As paredes chamadas depois desta sala viram o lado escolhido para dentro dela.
            _roomCenter = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            _wainscot = wainscot;

            Solid(parent, "Piso (colisão)", new Vector3((x0 + x1) * 0.5f, -0.2f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.2f, z1 - z0), _layerGround);

            Transform tiles = Group("Piso", parent).transform;
            Tiles(tiles, floor, x0, x1, z0, z1, 0f);

            Transform ceiling = Group("Teto", parent).transform;
            Tiles(ceiling, roof, x0, x1, z0, z1, height);
        }

        private static void Tiles(Transform parent, string path, float x0, float x1, float z0, float z1, float y, float target = 2f)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Tutorial: prefab não encontrado: {path}");
                return;
            }

            Bounds native = NativeBounds(prefab);
            if (native.size.x < 0.01f || native.size.z < 0.01f) return;

            int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / target));
            int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / target));
            float sx = (x1 - x0) / nx;
            float sz = (z1 - z0) / nz;
            Vector3 scale = new Vector3(sx / native.size.x, 1f, sz / native.size.z);
            Vector3 offset = Vector3.Scale(native.center, scale);

            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                tile.transform.localScale = scale;
                Vector3 center = new Vector3(x0 + sx * (i + 0.5f), y, z0 + sz * (j + 0.5f));
                tile.transform.position = center - new Vector3(offset.x, native.min.y, offset.z);
                DisableColliders(tile);
                GameObjectUtility.SetStaticEditorFlags(tile, StaticEditorFlags.BatchingStatic);
            }
        }

        private static readonly Dictionary<GameObject, Bounds> NativeBoundsCache = new();

        // Sala da vez (a última passada a Room) e o lado da parede que ela mostra: papel de parede
        // vermelho (salas) ou lambri com papel escuro (corredores e saguões).
        private static Vector2 _roomCenter;
        private static bool _wainscot;

        private static Bounds NativeBounds(GameObject prefab)
        {
            if (NativeBoundsCache.TryGetValue(prefab, out Bounds cached)) return cached;

            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            try
            {
                Bounds bounds = RendererBounds(probe);
                NativeBoundsCache[prefab] = bounds;
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Parede reta ao longo de X (<paramref name="alongX"/>, em z = <paramref name="fixedCoord"/>) ou
        /// de Z (em x = <paramref name="fixedCoord"/>), de <paramref name="a"/> a <paramref name="b"/>.
        /// Colisão por caixas invisíveis; o visual é peça de parede do jogo (2, 3 ou 4 m, esticada para
        /// fechar a medida). Acima de 3 m, uma segunda fileira.
        /// </summary>
        private static void Wall(Transform parent, bool alongX, float fixedCoord, float a, float b, float height, params Opening[] openings)
        {
            Transform wall = Group(alongX ? $"Parede z={fixedCoord:0.#}" : $"Parede x={fixedCoord:0.#}", parent).transform;
            Opening[] sorted = openings.OrderBy(o => o.Center).ToArray();

            // Colisão.
            float cursor = a;

            foreach (Opening opening in sorted)
            {
                if (opening.Blocked) continue;

                float start = opening.Center - opening.Width * 0.5f;
                float end = opening.Center + opening.Width * 0.5f;

                if (start > cursor + 0.01f) WallSolid(wall, alongX, fixedCoord, cursor, start, 0f, height);
                if (opening.Height < height - 0.01f) WallSolid(wall, alongX, fixedCoord, start, end, opening.Height, height);

                cursor = end;
            }

            if (b > cursor + 0.01f) WallSolid(wall, alongX, fixedCoord, cursor, b, 0f, height);

            // Visual.
            cursor = a;

            foreach (Opening opening in sorted)
            {
                float start = opening.Center - opening.PieceLength * 0.5f;
                float end = opening.Center + opening.PieceLength * 0.5f;

                if (start > cursor + 0.01f) WallVisual(wall, alongX, fixedCoord, cursor, start, height);

                if (opening.Piece != null)
                {
                    WallPiece(wall, opening.Piece, opening.PieceLength, alongX, fixedCoord, opening.Center, opening.PieceLength, 0f, 1f);
                    if (height > WallPieceHeight + 0.05f) WallUpper(wall, alongX, fixedCoord, start, end, height);
                }
                else if (opening.Height < height - 0.01f)
                {
                    WallUpper(wall, alongX, fixedCoord, start, end, height);
                }

                cursor = end;
            }

            if (b > cursor + 0.01f) WallVisual(wall, alongX, fixedCoord, cursor, b, height);
        }

        private static void WallSolid(Transform parent, bool alongX, float fixedCoord, float from, float to, float y0, float y1)
        {
            float mid = (from + to) * 0.5f;
            float length = to - from;

            Vector3 bottom = alongX ? new Vector3(mid, y0, fixedCoord) : new Vector3(fixedCoord, y0, mid);
            Vector3 size = alongX ? new Vector3(length, y1 - y0, Thickness) : new Vector3(Thickness, y1 - y0, length);
            Solid(parent, "Parede (colisão)", bottom, size, _layerWalls);
        }

        private static void WallVisual(Transform parent, bool alongX, float fixedCoord, float from, float to, float height)
        {
            WallRun(parent, alongX, fixedCoord, from, to, 0f, Mathf.Min(1f, height / WallPieceHeight));
            if (height > WallPieceHeight + 0.05f) WallUpper(parent, alongX, fixedCoord, from, to, height);
        }

        private static void WallUpper(Transform parent, bool alongX, float fixedCoord, float from, float to, float height)
        {
            WallRun(parent, alongX, fixedCoord, from, to, WallPieceHeight, (height - WallPieceHeight) / WallPieceHeight);
        }

        private static void WallRun(Transform parent, bool alongX, float fixedCoord, float from, float to, float y, float heightScale)
        {
            float length = to - from;
            if (length < 0.05f) return;

            int count = Mathf.Max(1, Mathf.CeilToInt(length / 4f - 0.01f));
            float piece = length / count;

            string path;
            float native;

            if (piece > 3.5f) { path = H + "SM_Sophisticated_Wall_4m.prefab"; native = 4f; }
            else if (piece > 2.5f) { path = H + "SM_Sophisticated_Wall_3m.prefab"; native = 3f; }
            else { path = H + "SM_Sophisticated_Wall_2m.prefab"; native = 2f; }

            for (int i = 0; i < count; i++)
            {
                WallPiece(parent, path, native, alongX, fixedCoord, from + piece * (i + 0.5f), piece, y, heightScale);
            }
        }

        /// <summary>Peça de parede do jogo (comprimento no eixo Z local), esticada e centrada no trecho.</summary>
        private static void WallPiece(Transform parent, string path, float native, bool alongX, float fixedCoord, float center, float length, float y, float heightScale)
        {
            // A face vermelha das peças é o +X local; o lambri, o -X. Vira a face da sala para dentro dela.
            Vector3 inward = alongX
                ? new Vector3(0f, 0f, Mathf.Sign(_roomCenter.y - fixedCoord))
                : new Vector3(Mathf.Sign(_roomCenter.x - fixedCoord), 0f, 0f);
            float yaw = Face(inward, true) + (_wainscot ? 180f : 0f);

            GameObject piece = Spawn(parent, path, yaw, new Vector3(1f, heightScale, length / native));
            if (piece == null) return;

            Fit(piece, alongX ? new Vector3(center, y, fixedCoord) : new Vector3(fixedCoord, y, center));
            DisableColliders(piece);
        }

        /// <summary>Porta do jogo no vão de uma parede ao longo de X (folha de frente para Z) ou de Z.</summary>
        private static Door DoorAt(Transform parent, string path, float x, float z, bool alongX)
        {
            GameObject door = Spawn(parent, path, alongX ? 0f : 90f);
            if (door == null) return null;

            EnsureNetworked(door);
            Fit(door, new Vector3(x, 0f, z));
            return door.GetComponent<Door>();
        }

        /// <summary>
        /// Passagem para a próxima lição: porta do jogo trancada (precisa do <see cref="DoorGap"/> na
        /// parede ao longo de X em <paramref name="z"/>) e a luz de emergência em cima, do lado de quem
        /// chega (sempre de -Z).
        /// </summary>
        private static void Gate(Transform parent, string id, float x, float z)
        {
            Door door = DoorAt(parent, Doors + "Door_Black.prefab", x, z, true);

            GameObject gate = new GameObject($"Portão · {id}");
            gate.transform.SetParent(parent, false);
            gate.transform.localPosition = new Vector3(x, 0f, z);
            gate.transform.localRotation = Quaternion.LookRotation(Vector3.back);

            Transform approach = Marker(gate.transform, "Lado de quem chega", Vector3.zero, Vector3.forward);
            approach.position = new Vector3(x, 1f, z - 1.6f);

            (Light light, Renderer renderer) = ExitLight(gate.transform, new Vector3(x, 2.3f, z - Thickness * 0.5f - 0.03f), Vector3.back, _emergencyLamp, new Color(1f, 0.06f, 0.04f));

            gate.AddComponent<TutorialDoorGate>().Configure(
                id, door, approach,
                light != null ? new[] { light } : new Light[0],
                renderer != null ? new[] { renderer } : new Renderer[0]);
        }

        /// <summary>
        /// Luz de saída/emergência do jogo (SM_Exit_Light) na parede, virada para <paramref name="facing"/>.
        /// Sai do LightSabotage: o apagão do monstro não mexe nela.
        /// </summary>
        private static (Light, Renderer) ExitLight(Transform parent, Vector3 position, Vector3 facing, Material material, Color color)
        {
            GameObject lamp = Spawn(parent, H + "SM_Exit_Light.prefab", Face(facing));
            if (lamp == null) return (null, null);

            PrefabUtility.UnpackPrefabInstance(lamp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Bounds bounds = RendererBounds(lamp);
            lamp.transform.position += position - bounds.center;

            foreach (LightSabotage sabotage in lamp.GetComponentsInChildren<LightSabotage>(true)) Object.DestroyImmediate(sabotage);

            Light light = lamp.GetComponentInChildren<Light>(true);

            if (light != null)
            {
                light.color = color;
                light.intensity = 2.2f;
                light.range = 6f;
                light.enabled = true;
            }

            Renderer renderer = lamp.GetComponentInChildren<Renderer>(true);
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            return (light, renderer);
        }

        /// <summary>
        /// Texto da tecla no marcador de interação. Override só nesta cena: no prefab do jogo o padrão
        /// é "Pegar", que não serve para quadro de luz.
        /// </summary>
        private static void SetMarkerLabel(GameObject go, string label)
        {
            if (go == null) return;

            foreach (InteractionMarker marker in go.GetComponentsInChildren<InteractionMarker>(true))
            {
                SerializedObject so = new SerializedObject(marker);
                so.FindProperty("actionLabel").stringValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureNetworked(GameObject go)
        {
            if (go.GetComponentInParent<NetworkObject>(true) == null) go.AddComponent<NetworkObject>();
        }

        /// <summary>Luminária do jogo (com LightSabotage) presa no teto.</summary>
        private static void Lamp(Transform parent, Vector3 floorPoint, float ceiling)
        {
            GameObject lamp = PlaceAt(parent, H + "SM_Hallway_Light_Roof.prefab", floorPoint, 0f);
            if (lamp == null) return;

            Bounds bounds = RendererBounds(lamp);
            lamp.transform.position += new Vector3(0f, ceiling - 0.01f - bounds.max.y, 0f);

            // Override só nesta cena: a luminária do jogo (2,6 / 8 m) foi feita para corredor estreito.
            bool tall = ceiling > Height + 0.1f;

            foreach (Light light in lamp.GetComponentsInChildren<Light>(true))
            {
                light.intensity = tall ? 15f : 10f;
                light.range = tall ? 18f : 14f;
                EditorUtility.SetDirty(light);
            }
        }

        /// <summary>Luz vermelha que só acende quando o monstro apaga as luzes.</summary>
        private static void EmergencyLamp(Transform parent, Vector3 position)
        {
            GameObject go = new GameObject("Luz de emergência (apagão)");
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

            // Sem a caixinha vermelha de "lâmpada": foi tirada à mão na cena (2026-11-24); só a luz.
        }

        /// <summary>
        /// A placa ACHADOS E PERDIDOS da cena Game (tábua, moldura e texto Lalezar laranja), com a
        /// frente virada para <paramref name="yaw"/>.
        /// </summary>
        private static Transform LostAndFoundSign(Transform parent, Vector3 position, float yaw)
        {
            GameObject sign = new GameObject("Achados e perdidos");
            sign.transform.SetParent(parent, false);
            sign.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            Material board = AssetDatabase.LoadAssetAtPath<Material>(TutorialPlaques.BoardMaterialPath);
            Material frame = AssetDatabase.LoadAssetAtPath<Material>(TutorialPlaques.FrameMaterialPath);

            SignCube(sign.transform, "Tabua", Vector3.zero, new Vector3(1.8f, 0.44f, 0.04f), board);

            Transform moldura = Group("Moldura", sign.transform).transform;
            SignCube(moldura, "Borda", new Vector3(0f, 0.21f, 0f), new Vector3(1.8f, 0.03f, 0.05f), frame);
            SignCube(moldura, "Borda", new Vector3(0f, -0.21f, 0f), new Vector3(1.8f, 0.03f, 0.05f), frame);
            SignCube(moldura, "Borda", new Vector3(-0.89f, 0f, 0f), new Vector3(0.03f, 0.44f, 0.05f), frame);
            SignCube(moldura, "Borda", new Vector3(0.89f, 0f, 0f), new Vector3(0.03f, 0.44f, 0.05f), frame);

            GameObject textGo = new GameObject("Texto", typeof(RectTransform));
            textGo.transform.SetParent(sign.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, 0.021f);
            textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            TextMeshPro text = textGo.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TutorialPlaques.FontPath);
            text.text = "ACHADOS E PERDIDOS";
            text.color = TutorialPlaques.TitleColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.5f;
            text.fontSizeMax = 1.75f;
            text.rectTransform.sizeDelta = new Vector2(1.62f, 0.34f);

            return sign.transform;
        }

        private static void SignCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>Grupo de marcas de chão que some quando a etapa <paramref name="id"/> termina.</summary>
        private static Transform FloorHint(Transform parent, string id)
        {
            GameObject group = Group($"Marca de chão · {id}", parent);
            group.AddComponent<TutorialFloorHint>().Configure(id);
            return group.transform;
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

        /// <summary>
        /// Botão de parede com o mesmo modelo do puzzle de luz (SM_Button do LampTotem: base preta,
        /// botão vermelho). O modelo tem o botão para +Y; aqui ele é deitado para apontar para
        /// <paramref name="facing"/>.
        /// </summary>
        private static TutorialButton Button(Transform parent, Vector3 position, Vector3 facing)
        {
            const float scale = 1.8f;

            GameObject root = new GameObject("Botão escondido");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.LookRotation(facing);
            root.layer = _layerInteractable;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.2f * scale + 0.04f, 0.2f * scale + 0.04f, 0.1f);
            collider.center = new Vector3(0f, 0f, 0.04f);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Missions_objects/SM_Button.fbx");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "SM_Button";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // +Y do modelo -> para fora da parede
            model.transform.localScale = Vector3.one * scale;

            Material holderMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Assets_Preview/Black.mat");
            Material capMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/GYM/Gym/Materials/Gym_Pad_Red.mat");

            Renderer capRenderer = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = _layerInteractable;
                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer == null) continue;

                bool isCap = t != model.transform;
                renderer.sharedMaterial = isCap ? capMaterial : holderMaterial;
                if (isCap) capRenderer = renderer;
            }

            Outline outline = capRenderer != null ? capRenderer.gameObject.AddComponent<Outline>() : model.AddComponent<Outline>();
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
            button.Configure("chat_button", capRenderer != null ? capRenderer.transform : null, capRenderer);
            return button;
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

        /// <summary>Placa com o número e o nome da lição, na parede do fundo, ao lado da porta (vista de quem chega).</summary>
        private static void Sign(Transform labels, string id, string title, string subtitle, float wallZ, float x)
        {
            string text = string.IsNullOrEmpty(subtitle) ? $"<b>{title}</b>" : $"<b>{title}</b>\n<size=60%>{subtitle}</size>";
            Label(FloorHint(labels, id), text, new Vector3(x, 2.05f, wallZ - Thickness * 0.5f - 0.02f), FacingViewer(Vector3.back), 1.9f, 3.6f, new Color(1f, 0.85f, 0.35f));
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

        /// <summary>
        /// Yaw para um objeto do jogo ficar de frente para <paramref name="direction"/>. Os móveis têm a
        /// frente em +Z local; quadros, cartazes e grades (finos no X) têm a frente em +X.
        /// </summary>
        private static float Face(Vector3 direction, bool frontIsX = false)
        {
            direction.y = 0f;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            return frontIsX ? yaw - 90f : yaw;
        }

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

        // ================================================================== caixas (marcas, botão) e colisores

        /// <summary>Colisor invisível dado pelo centro da base.</summary>
        private static GameObject Solid(Transform parent, string name, Vector3 bottomCenter, Vector3 size, int layer)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = bottomCenter + Vector3.up * (size.y * 0.5f);
            go.layer = layer;

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
            return go;
        }

        /// <summary>
        /// Acabamento das laterais do palco. Na Game o SM_Stage encosta nas paredes do auditório e não
        /// tem face nos lados: aqui, solto na sala, dava para ver por baixo dele. Cada lado, da escada
        /// do canto até o fundo, ganha um painel na madeira do piso do corredor, com friso em cima e
        /// rodapé embaixo.
        /// </summary>
        private static void StageSides(Transform room, Bounds stage, float stageTop)
        {
            // Atrás da escada do canto o palco já tem altura cheia: 3,44 m depois da frente (escala 60%).
            const float stairBack = 3.44f;
            const float panel = 0.04f;

            Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Scenary/Hallway/Ground/M_GroundHallway.mat");
            float zFrom = stage.min.z + stairBack;
            float zTo = stage.max.z;
            float length = zTo - zFrom;
            float zMid = (zFrom + zTo) * 0.5f;

            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side > 0f ? stage.max.x : stage.min.x;
                string name = side > 0f ? "direita" : "esquerda";

                Box(room, $"Palco · lateral {name}", new Vector3(edge + side * panel * 0.5f, 0f, zMid),
                    new Vector3(panel, stageTop, length), wood, _layerGround, true, 1f, true);

                // Friso: passa um pouco do painel e cobre a quina de cima.
                Box(room, $"Palco · friso {name}", new Vector3(edge + side * 0.04f, stageTop - 0.05f, zMid),
                    new Vector3(0.08f, 0.07f, length + 0.04f), wood, _layerGround, false, 1f, true);

                Box(room, $"Palco · rodapé {name}", new Vector3(edge + side * 0.035f, 0f, zMid),
                    new Vector3(0.07f, 0.09f, length), wood, _layerGround, false, 1f, true);
            }
        }

        /// <summary>Caixa visível dada pelo centro da base, com UV em metros (marcas no chão, botão).</summary>
        private static GameObject Box(Transform parent, string name, Vector3 bottomCenter, Vector3 size, Material material, int layer, bool collider = true, float uvMeters = UvMeters, bool swapUv = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = bottomCenter + Vector3.up * (size.y * 0.5f);
            go.layer = layer;

            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, uvMeters, swapUv);
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

        private static Mesh BoxMesh(Vector3 size, float uvMeters = UvMeters, bool swapUv = false)
        {
            string key = $"{size.x:0.###}x{size.y:0.###}x{size.z:0.###}" + (Mathf.Approximately(uvMeters, UvMeters) ? "" : $"@{uvMeters:0.###}") + (swapUv ? "r" : "");
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

                float uw = width / uvMeters;
                float vh = height / uvMeters;
                // swapUv deita a textura (tábuas do piso correndo ao longo da peça, e não em pé).
                uvs.Add(swapUv ? new Vector2(0f, 0f) : new Vector2(0f, 0f));
                uvs.Add(swapUv ? new Vector2(0f, uw) : new Vector2(uw, 0f));
                uvs.Add(swapUv ? new Vector2(vh, uw) : new Vector2(uw, vh));
                uvs.Add(swapUv ? new Vector2(vh, 0f) : new Vector2(0f, vh));

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
            GameObject instance = Spawn(parent, path, yaw);
            if (instance != null) instance.transform.localPosition = position;
            return instance;
        }

        /// <summary>Instancia o prefab (ou modelo) na origem do pai, girado e escalado.</summary>
        private static GameObject Spawn(Transform parent, string path, float yaw, Vector3? scale = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogWarning($"Tutorial: prefab não encontrado: {path}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (scale.HasValue) instance.transform.localScale = scale.Value;
            return instance;
        }

        /// <summary>Move o objeto para o centro do que se vê dele ficar em (x, z) e a base em y, qualquer que seja o pivô.</summary>
        private static void Fit(GameObject instance, Vector3 bottomCenter)
        {
            if (instance == null) return;

            Bounds bounds = RendererBounds(instance);
            if (bounds.size == Vector3.zero)
            {
                instance.transform.position = bottomCenter;
                return;
            }

            instance.transform.position += new Vector3(bottomCenter.x - bounds.center.x, bottomCenter.y - bounds.min.y, bottomCenter.z - bounds.center.z);
        }

        /// <summary>
        /// Fila de pedestais com corda (como na bilheteria): poste sozinho em <paramref name="from"/> e
        /// um pedestal com corda a cada 1,15 m (o tamanho da corda do modelo) indo até perto de
        /// <paramref name="to"/>, cada corda presa no poste de trás. Assim nenhuma corda fica solta.
        /// </summary>
        private static void Queue(Transform parent, Vector3 from, Vector3 to)
        {
            const float span = 1.15f;

            Vector3 direction = to - from;
            direction.y = 0f;
            int count = Mathf.Max(1, Mathf.RoundToInt(direction.magnitude / span));
            direction.Normalize();

            GameObject first = Spawn(parent, B + "SM_Stanchion_Solo.prefab", 0f);
            if (first != null) first.transform.position = from;

            for (int i = 1; i <= count; i++)
            {
                // O pivô do pedestal é o centro do poste e a corda sai para o -X local: virando o +X
                // para a frente da fila, a corda alcança o poste anterior.
                GameObject post = Spawn(parent, B + "SM_Stanchion.prefab", Face(direction, true));
                if (post != null) post.transform.position = from + direction * (span * i);
            }
        }

        /// <summary>Pedaço de cano solto (modelo .fbx) com o material dos canos do jogo, como na sala de canos.</summary>
        private static void PipeProp(Transform parent, string path, Vector3 bottomCenter, float yaw)
        {
            GameObject pipe = Prop(parent, path, bottomCenter, yaw);
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Missions/Pipes/M_Pipes.mat");
            if (pipe == null || material == null) return;

            foreach (Renderer renderer in pipe.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
        }

        /// <summary>Móvel ou objeto do jogo apoiado em <paramref name="bottomCenter"/>.</summary>
        private static GameObject Prop(Transform parent, string path, Vector3 bottomCenter, float yaw, float scale = 1f)
        {
            GameObject instance = Spawn(parent, path, yaw, Mathf.Approximately(scale, 1f) ? null : Vector3.one * scale);
            Fit(instance, bottomCenter + Vector3.up * 0.002f);
            return instance;
        }

        /// <summary>
        /// Quadro, cartaz, mural ou placa preso na parede, com o centro em <paramref name="center"/>.
        /// Quadros e cartazes são finos no X com a frente em -X; placas finas no Z têm a frente em +Z.
        /// </summary>
        private static GameObject WallProp(Transform parent, string path, Vector3 center, Vector3 facing, bool thinInZ = false)
        {
            GameObject instance = Spawn(parent, path, thinInZ ? Face(facing) : Face(facing, true) + 180f);
            if (instance == null) return null;

            Bounds bounds = RendererBounds(instance);
            instance.transform.position += center - bounds.center;
            return instance;
        }

        /// <summary>Encosta a base do que se vê do prefab em <paramref name="bottomCenter"/>, qualquer que seja o pivô.</summary>
        private static GameObject PlaceOnFloor(Transform parent, string path, Vector3 bottomCenter, float yaw)
        {
            GameObject instance = Spawn(parent, path, yaw);
            Fit(instance, bottomCenter + Vector3.up * 0.002f);
            return instance;
        }

        /// <summary>
        /// Desliga os colisores de uma peça visual: a colisão daquele trecho é a caixa invisível do
        /// gerador (mesma medida para o jogador e para o NavMesh).
        /// </summary>
        private static void DisableColliders(GameObject go)
        {
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
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
            RenderSettings.ambientSkyColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.ambientEquatorColor = new Color(0.04f, 0.035f, 0.032f);
            RenderSettings.ambientGroundColor = new Color(0.014f, 0.012f, 0.011f);
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

            surface.BuildNavMesh();

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
            _layerInteractable = LayerMask.NameToLayer("Interactable");
            _layerIgnoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        }

        private static void LoadMaterials()
        {
            _metal = Lit("Tutorial_Metal", new Color(0.27f, 0.28f, 0.3f), 0.55f, 0.6f);

            _unlitYellow = Unlit("Tutorial_Amarelo", new Color(1f, 0.8f, 0.2f), true);
            _unlitGreen = Unlit("Tutorial_Verde", new Color(0.3f, 0.95f, 0.45f), true);
            _unlitRed = Unlit("Tutorial_Vermelho", new Color(1f, 0.15f, 0.1f), true);
            _unlitBlack = Unlit("Tutorial_Preto", new Color(0.01f, 0.01f, 0.01f), true);
            _gateLamp = Unlit("Tutorial_LuzPortao", Color.white, true);

            // Luz de emergência das portas: a cor vem do TutorialDoorGate (vermelha/verde) em tempo de jogo.
            _emergencyLamp = Emissive("Tutorial_LuzEmergencia", Color.white);
            _exitLamp = Emissive("Tutorial_LuzSaida", new Color(0.1f, 1f, 0.25f));

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Acme-Regular SDF.asset");
        }

        private static Material Emissive(string name, Color color)
        {
            Material material = Lit(name, color, 0.6f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", color * 3f);
            EditorUtility.SetDirty(material);
            return material;
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

        private static Sprite HudSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/" + path + ".png");

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
