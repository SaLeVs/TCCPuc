using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Audio;
using LevelDesign.Gym.VoiceLab;
using Network;
using Objects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LevelDesign.Gym.Building
{
    /// <summary>
    /// Builds the voice lab: a geometry-only copy of Game.unity in a scene of its own, opened next to
    /// the GYM, plus a stand-in companion who talks through the same audio chain as a remote player.
    ///
    /// <para>Game.unity is only read. The copy keeps what sound and sight need — renderers, colliders
    /// on their layers, lights, Steam Audio geometry, the voice zones — and drops every gameplay
    /// script, network object and camera, so it never fights the GYM's own session. Doors keep their
    /// swing through GymVoiceLabDoor. Run it again whenever the map changes.</para>
    /// </summary>
    public static class GymVoiceLabBuilder
    {
        public const string LabScenePath = "Assets/GYM/Scenes/GYM_VozLab.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string GymScenePath = "Assets/GYM/Scenes/GYM.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string HostManagerPrefabPath = "Assets/Prefabs/Network/HostManager.prefab";
        private const string SpeechClipPath = "Assets/GYM/VoiceLab/Audio/Lab_Fala.wav";
        private const string DonationClipPath = "Assets/GYM/VoiceLab/Audio/Lab_Doacao.wav";
        private const string MaterialPath = "Assets/GYM/VoiceLab/Materials/Companheiro.mat";

        /// <summary>Clear of the GYM's own floor (x −62..62, z −30..34) and of the map's (x −41..90).</summary>
        public static readonly Vector3 ReplicaOffset = new Vector3(250f, 0f, 0f);

        // Game.unity roots that are not level geometry: managers, spawns, the monster, UI, pickups.
        private static readonly string[] SkippedRoots =
        {
            "MANAGERS", "Main Camera", "EventSystem", "Global Volume", "ChatEventHub", "MissionManager",
            "SpawnRoomManager", "AudienceManager", "MissionRegisterManager", "SpawnManager", "LostItemsSpawns",
            "SfxManager", "DonationManager", "DonationAudioManager", "EletricCircuitManager", "Sectors",
            "ENVIRONMENT", "Monster", "SpawnRoomPoints", "CircuitSpawnPoints", "MainStage", "ObjectAudience",
            "NavMesh", "CANVASES", "FadeScreen", "NoiseDebugger", "LightDistanceCuller", "FlashlightBatteryPickable",
        };

        private readonly struct ScenarioSpec
        {
            public readonly string Name;
            public readonly string Hint;
            public readonly Vector2 Speaker;
            public readonly float SpeakerYaw;
            public readonly Vector2 Player;
            public readonly float PlayerYaw;
            public readonly GymVoiceLab.Facing Facing;

            public ScenarioSpec(string name, string hint, Vector2 speaker, float speakerYaw, Vector2 player, float playerYaw, GymVoiceLab.Facing facing)
            {
                Name = name;
                Hint = hint;
                Speaker = speaker;
                SpeakerYaw = speakerYaw;
                Player = player;
                PlayerYaw = playerYaw;
                Facing = facing;
            }
        }

        // Game.unity coordinates (x, z); heights come from the floor found under each point.
        private static readonly ScenarioSpec[] Scenarios =
        {
            new ScenarioSpec("Mesma sala → porta → corredor",
                "Saia pela porta ao norte e siga o corredor: repare no degrau de volume quando a parede entra entre vocês.",
                new Vector2(32.2f, -1.5f), 0f, new Vector2(32.2f, 1.5f), 0f, GymVoiceLab.Facing.TowardsPlayer),
            new ScenarioSpec("Corredor reto (visão livre)",
                "Ande para o norte pelo corredor olhando a distância: aqui só a curva e a borda do alcance agem.",
                new Vector2(30.9f, 7f), 0f, new Vector2(30.9f, 9f), 0f, GymVoiceLab.Facing.TowardsPlayer),
            new ScenarioSpec("Virando o corredor",
                "Siga até a curva (z ≈ 44) e vire para o leste: a parede corta a linha de visão.",
                new Vector2(30.9f, 34f), 0f, new Vector2(30.9f, 37f), 0f, GymVoiceLab.Facing.TowardsPlayer),
            new ScenarioSpec("Sala vizinha (através da parede)",
                "Salas vizinhas. Abra e feche as portas entre elas (O perto da porta) e compare.",
                new Vector2(36f, -2.5f), 90f, new Vector2(45f, -2.5f), 270f, GymVoiceLab.Facing.TowardsPlayer),
            new ScenarioSpec("Auditório (palco → plateia)",
                "Companheiro no palco, dentro da zona com reverb. Suba pela plateia e saia pelas portas.",
                new Vector2(10.5f, 30.5f), 0f, new Vector2(10.5f, 38f), 180f, GymVoiceLab.Facing.TowardsPlayer),
        };

        [MenuItem("Tools/TCC/Level Design/Laboratório de voz/Gerar laboratório (réplica do Game)", priority = 40)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string log = Build();
            Debug.Log(log);
            EditorUtility.DisplayDialog("Laboratório de voz", "Laboratório gerado em " + LabScenePath +
                                        ".\n\nAbra com Tools/TCC/Level Design/Laboratório de voz/Abrir GYM + laboratório de voz.", "OK");
        }

        [MenuItem("Tools/TCC/Level Design/Laboratório de voz/Abrir GYM + laboratório de voz", priority = 41)]
        public static void OpenWithGym()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScenePath) == null)
            {
                EditorUtility.DisplayDialog("Laboratório de voz", "O laboratório ainda não foi gerado. Use \"Gerar laboratório\" primeiro.", "OK");
                return;
            }

            OpenScenes();
        }

        public static void OpenScenes()
        {
            Scene gym = EditorSceneManager.OpenScene(GymScenePath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(gym);
        }

        public static string Build()
        {
            var log = new StringBuilder();

            PrepareClips(log);

            Scene existing = SceneManager.GetSceneByPath(LabScenePath);
            if (existing.IsValid() && existing.isLoaded) EditorSceneManager.CloseScene(existing, true);

            Scene game = SceneManager.GetSceneByPath(GameScenePath);
            bool openedGame = false;

            if (!game.IsValid() || !game.isLoaded)
            {
                game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                openedGame = true;
            }

            Scene lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                var labRoot = new GameObject("Laboratório de voz");
                SceneManager.MoveGameObjectToScene(labRoot, lab);

                var replica = new GameObject("Réplica do Game.unity (só geometria e som)");
                replica.transform.SetParent(labRoot.transform, false);
                replica.transform.position = ReplicaOffset;

                CopyGeometry(game, replica.transform, log);

                Physics.SyncTransforms();

                Transform companion = BuildCompanion(labRoot.transform, log);
                BuildController(labRoot.transform, companion, log);

                if (!EditorSceneManager.SaveScene(lab, LabScenePath))
                {
                    log.AppendLine("ERRO: não salvou " + LabScenePath);
                }
                else
                {
                    log.AppendLine("Salvo: " + LabScenePath);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(lab, true);
                if (openedGame) EditorSceneManager.CloseScene(game, true);
            }

            return log.ToString();
        }

        // ---------------------------------------------------------------- replica

        private static void CopyGeometry(Scene game, Transform replica, StringBuilder log)
        {
            int copiedRoots = 0;
            int doors = 0;
            var removed = new Dictionary<string, int>();

            foreach (GameObject root in game.GetRootGameObjects())
            {
                if (!root.activeSelf) continue;
                if (SkippedRoots.Any(skip => root.name.StartsWith(skip, StringComparison.Ordinal))) continue;

                bool isZone = root.GetComponentInChildren<VoiceZoneTrigger>(true) != null;
                bool hasGeometry = root.GetComponentInChildren<Renderer>(true) != null || root.GetComponentInChildren<Collider>(true) != null;

                if (!isZone && !hasGeometry) continue;

                GameObject copy = Object.Instantiate(root);
                copy.name = root.name;
                SceneManager.MoveGameObjectToScene(copy, replica.gameObject.scene);
                copy.transform.SetParent(replica, false);

                doors += ConvertDoors(copy);
                Strip(copy, removed);

                copiedRoots++;
            }

            int renderers = replica.GetComponentsInChildren<Renderer>(true).Length;
            int colliders = replica.GetComponentsInChildren<Collider>(true).Length;
            int zones = replica.GetComponentsInChildren<VoiceZoneTrigger>(true).Length;
            int geometry = replica.GetComponentsInChildren<Component>(true).Count(c => c != null && c.GetType().Name == "SteamAudioGeometry");

            log.AppendLine($"Réplica: {copiedRoots} raízes do Game, {renderers} renderers, {colliders} colliders, {doors} portas, {zones} zonas de voz, {geometry} SteamAudioGeometry.");
            log.AppendLine("Removidos: " + string.Join(", ", removed.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}")));
        }

        /// <summary>Replaces each game Door by the lab's swing-only door, read before the Door is stripped.</summary>
        private static int ConvertDoors(GameObject copy)
        {
            int count = 0;

            foreach (Door door in copy.GetComponentsInChildren<Door>(true))
            {
                var so = new SerializedObject(door);
                var leaves = new List<GymVoiceLabDoor.Leaf>();

                float closed = so.FindProperty("closedAngle").floatValue;
                float open = so.FindProperty("openSideAAngle").floatValue;

                SerializedProperty hinge = so.FindProperty("hingeAxis");
                string axisName = hinge.enumValueIndex >= 0 && hinge.enumValueIndex < hinge.enumNames.Length ? hinge.enumNames[hinge.enumValueIndex] : "Up";
                Vector3 axis = axisName == "Right" ? Vector3.right : axisName == "Forward" ? Vector3.forward : Vector3.up;

                if (so.FindProperty("doorRigidbody").objectReferenceValue is Rigidbody main)
                {
                    leaves.Add(new GymVoiceLabDoor.Leaf { pivot = main.transform, closedYaw = closed, mirror = 1f });
                }

                SerializedProperty extras = so.FindProperty("extraLeaves");

                for (int i = 0; extras != null && i < extras.arraySize; i++)
                {
                    SerializedProperty extra = extras.GetArrayElementAtIndex(i);
                    if (!(extra.FindPropertyRelative("leafRigidbody").objectReferenceValue is Rigidbody body)) continue;

                    leaves.Add(new GymVoiceLabDoor.Leaf
                    {
                        pivot = body.transform,
                        closedYaw = extra.FindPropertyRelative("closedAngle").floatValue,
                        mirror = extra.FindPropertyRelative("mirror").floatValue < 0f ? -1f : 1f,
                    });
                }

                var blocker = so.FindProperty("closedBlocker").objectReferenceValue as Collider;

                var labDoor = door.gameObject.AddComponent<GymVoiceLabDoor>();
                labDoor.Configure(leaves.ToArray(), closed, open, axis, blocker);
                count++;
            }

            return count;
        }

        private static bool Keep(Component component)
        {
            switch (component)
            {
                case Transform _:
                case MeshFilter _:
                case Renderer _:
                case LODGroup _:
                case Collider _:
                case Light _:
                case LightProbeGroup _:
                case ReflectionProbe _:
                case ParticleSystem _:
                case AudioSource _:
                case AudioReverbZone _:
                case Canvas _:
                case CanvasRenderer _:
                case CanvasGroup _:
                case UnityEngine.UI.Graphic _:
                case VoiceZoneTrigger _:
                case GymVoiceLabDoor _:
                    return true;
            }

            string ns = component.GetType().Namespace ?? string.Empty;

            // Steam Audio's geometry and sources, and the URP data that rides on lights and decals.
            if (ns == "SteamAudio") return true;
            if (component is UnityEngine.Rendering.Universal.UniversalAdditionalLightData) return true;
            if (component is UnityEngine.Rendering.Universal.DecalProjector) return true;
            if (component is UnityEngine.Rendering.Volume) return true;

            return false;
        }

        /// <summary>
        /// Drops everything Keep() does not list, dependents first: a component another one still
        /// requires waits for a later pass.
        /// </summary>
        private static void Strip(GameObject root, Dictionary<string, int> removed)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (missing > 0) Count(removed, "(script ausente)", missing);
            }

            for (int pass = 0; pass < 8; pass++)
            {
                bool progress = false;
                bool waiting = false;

                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || Keep(component)) continue;

                    if (IsRequiredByAnother(component))
                    {
                        waiting = true;
                        continue;
                    }

                    Count(removed, component.GetType().Name, 1);
                    Object.DestroyImmediate(component);
                    progress = true;
                }

                if (!waiting || !progress) break;
            }

            // Whatever is left is static scenery now: no body may fall, roll or be pushed.
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
            }
        }

        private static bool IsRequiredByAnother(Component component)
        {
            foreach (Component other in component.GetComponents<Component>())
            {
                if (other == null || other == component) continue;

                foreach (RequireComponent require in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (Requires(require.m_Type0, component) || Requires(require.m_Type1, component) || Requires(require.m_Type2, component))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Requires(Type required, Component component)
        {
            if (required == null || !required.IsInstanceOfType(component)) return false;

            // Another component of the same kind would still satisfy the requirement.
            return component.GetComponents(required).Length <= 1;
        }

        private static void Count(Dictionary<string, int> removed, string key, int amount)
        {
            removed.TryGetValue(key, out int current);
            removed[key] = current + amount;
        }

        // ---------------------------------------------------------------- companion

        private static Transform BuildCompanion(Transform labRoot, StringBuilder log)
        {
            Material material = CompanionMaterial();

            var body = new GameObject("Companheiro (voz de teste)");
            body.transform.SetParent(labRoot, false);

            // Proportions of the Player prefab: capsule 1,43 m, camera at 1,0 m, mouth just under it.
            AddPart(body.transform, PrimitiveType.Capsule, new Vector3(0f, 0.62f, 0f), new Vector3(0.46f, 0.62f, 0.46f), material);
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(0f, 1.12f, 0f), Vector3.one * 0.34f, material);
            AddPart(body.transform, PrimitiveType.Cube, new Vector3(0f, 1.1f, 0.19f), new Vector3(0.09f, 0.09f, 0.14f), material);

            var mouth = new GameObject("Boca (VoiceAnchor)");
            mouth.transform.SetParent(body.transform, false);
            mouth.transform.localPosition = new Vector3(0f, 0.98f, 0.2f);

            // The map is a horror map: without its own light the companion is lost in the dark.
            var glow = new GameObject("Luz do companheiro");
            glow.transform.SetParent(body.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.7f, 0.4f);
            light.shadows = LightShadows.None;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var hostManager = AssetDatabase.LoadAssetAtPath<GameObject>(HostManagerPrefabPath);
            RemoteVoiceFilter voiceFilter = playerPrefab != null ? playerPrefab.GetComponentInChildren<RemoteVoiceFilter>(true) : null;
            VivoxManager vivox = hostManager != null ? hostManager.GetComponentInChildren<VivoxManager>(true) : null;

            var speaker = body.AddComponent<GymVoiceLabSpeaker>();
            speaker.Configure(voiceFilter, vivox, mouth.transform,
                AssetDatabase.LoadAssetAtPath<AudioClip>(SpeechClipPath),
                AssetDatabase.LoadAssetAtPath<AudioClip>(DonationClipPath));

            if (voiceFilter == null) log.AppendLine("AVISO: RemoteVoiceFilter não encontrado em " + PlayerPrefabPath);
            if (vivox == null) log.AppendLine("AVISO: VivoxManager não encontrado em " + HostManagerPrefabPath);

            return body.transform;
        }

        private static void AddPart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);

            // No collider: Steam Audio traces Default, so the companion's own body would muffle its voice.
            Object.DestroyImmediate(part.GetComponent<Collider>());

            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material CompanionMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "Companheiro" };
            material.color = new Color(1f, 0.55f, 0.1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, 0.55f, 0.1f));
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.35f, 0.16f, 0.02f));
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // ---------------------------------------------------------------- controller

        private static void BuildController(Transform labRoot, Transform companion, StringBuilder log)
        {
            var control = new GameObject("Controle do laboratório");
            control.transform.SetParent(labRoot, false);

            var points = new GameObject("Cenários");
            points.transform.SetParent(labRoot, false);

            var lab = control.AddComponent<GymVoiceLab>();
            var so = new SerializedObject(lab);

            so.FindProperty("speaker").objectReferenceValue = companion.GetComponent<GymVoiceLabSpeaker>();
            so.FindProperty("speakerBody").objectReferenceValue = companion;
            so.FindProperty("replicaOffset").vector3Value = ReplicaOffset;

            SerializedProperty list = so.FindProperty("scenarios");
            list.arraySize = Scenarios.Length;

            for (int i = 0; i < Scenarios.Length; i++)
            {
                ScenarioSpec spec = Scenarios[i];

                Transform speakerPoint = MakePoint(points.transform, $"{i + 1} {spec.Name} — companheiro", spec.Speaker, spec.SpeakerYaw, log);
                Transform playerPoint = MakePoint(points.transform, $"{i + 1} {spec.Name} — você", spec.Player, spec.PlayerYaw, log);

                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = spec.Name;
                element.FindPropertyRelative("hint").stringValue = spec.Hint;
                element.FindPropertyRelative("speakerPoint").objectReferenceValue = speakerPoint;
                element.FindPropertyRelative("playerPoint").objectReferenceValue = playerPoint;
                element.FindPropertyRelative("facing").enumValueIndex = (int)spec.Facing;

                if (i == 0) companion.SetPositionAndRotation(speakerPoint.position, speakerPoint.rotation);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A point on the replica's floor under the given Game.unity (x, z).</summary>
        private static Transform MakePoint(Transform parent, string pointName, Vector2 gameXZ, float yaw, StringBuilder log)
        {
            Vector3 top = new Vector3(gameXZ.x, 0f, gameXZ.y) + ReplicaOffset + Vector3.up * 30f;
            float y = ReplicaOffset.y;
            bool found = false;

            // Roofs are not on Ground, so the first Ground surface from above is the one you stand on —
            // the stage top on the stage, the tier itself in the seats.
            int ground = LayerMask.NameToLayer("Ground");
            RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == parent.gameObject.scene && h.normal.y > 0.7f)
                .OrderBy(h => h.distance)
                .ToArray();

            RaycastHit[] floors = hits.Where(h => h.collider.gameObject.layer == ground).ToArray();
            if (floors.Length == 0) floors = hits;

            if (floors.Length > 0)
            {
                y = floors[0].point.y;
                found = true;
                log.AppendLine($"  {pointName}: chão y={y - ReplicaOffset.y:0.00} ({LayerMask.LayerToName(floors[0].collider.gameObject.layer)}, {floors[0].collider.name})");
            }

            if (!found) log.AppendLine($"AVISO: sem chão sob {pointName} ({gameXZ.x}; {gameXZ.y}).");

            var point = new GameObject(pointName);
            point.transform.SetParent(parent, false);
            point.transform.SetPositionAndRotation(new Vector3(top.x, y + 0.05f, top.z), Quaternion.Euler(0f, yaw, 0f));

            return point.transform;
        }

        // ---------------------------------------------------------------- clips

        private static void PrepareClips(StringBuilder log)
        {
            foreach (string path in new[] { SpeechClipPath, DonationClipPath })
            {
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                {
                    log.AppendLine("AVISO: clipe não encontrado: " + path);
                    continue;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool changed = !importer.forceToMono || settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                               settings.compressionFormat != AudioCompressionFormat.PCM;

                if (!changed) continue;

                // GetData needs the samples in memory; PCM keeps the line exactly as generated.
                importer.forceToMono = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }
    }
}
