using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Enums;
using ScriptableObjects;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prototypes.Editor
{
    /// <summary>
    /// Everything the prototype levels use that is not the level itself, made once and kept in
    /// Assets/Prototypes: game-ready variants of the 14.7 m rooms, copies of the Hamlet missions and
    /// contract pointing at them. Nothing under Assets/Prefabs or Assets/Scriptable_objects changes.
    /// </summary>
    public static class PrototypeAssets
    {
        public const string Root = "Assets/Prototypes";
        public const string RoomsFolder = Root + "/Rooms";
        public const string DataFolder = Root + "/Data";
        public const string ContractFolder = DataFolder + "/Contract";
        public const string RoomDataFolder = DataFolder + "/Rooms";
        public const string BaseRoomDataFolder = RoomDataFolder + "/Base";
        public const string MissionRoomDataFolder = RoomDataFolder + "/Mission";
        public const string MissionDataFolder = DataFolder + "/Missions";
        public const string MaterialsFolder = Root + "/Materials";

        private const string SourceContract = "Assets/Scriptable_objects/Contracts/Contract Hamlet.asset";
        public const string ContractPath = ContractFolder + "/Contract Hamlet Prototipo.asset";

        // The backup rooms the missions were authored in, against the new 14.7 m rooms: same
        // relative spot in the room after this shift (cores aligned, see the level design notes).
        private static readonly Vector3 MissionShift = new Vector3(-6.19f, 0f, 4.84f);

        // Rooms whose new furniture did not move with the core: the box room's shelves, lockers and
        // dolly sit 0.104 / 1.108 m further on than MissionShift puts them, so the totem post-its hung
        // in the aisle and the boxes went into the shelves.
        private static readonly Dictionary<string, Vector3> RoomShifts = new Dictionary<string, Vector3>
        {
            { "MIssionBoxRoom", new Vector3(0.104f, 0f, 1.108f) },
        };

        // Spawn points still on nothing after the shift, because what held them was rearranged in the
        // new room: moved, in the entry's own space, onto the same piece of furniture. The entry gets a
        // variant in Assets/Prototypes; the original still serves the backup room in Game.
        private static readonly Dictionary<string, (string point, Vector3 localPosition)[]> EntryFixes = new Dictionary<string, (string, Vector3)[]>
        {
            {
                "MIssionBoxRoom", new[]
                {
                    ("BoxSpawnPoints/Box2", new Vector3(8.033f, 1.025f, 4.599f)),  // on the small carton of the pile, which moved to the corner
                    ("BoxSpawnPoints/Box4", new Vector3(-2.987f, 0.261f, 4.684f)), // bottom board of the shelf that moved to the corner
                }
            },
        };

        // Entries placed by hand in the new room instead of shifted from the backup one. The sound
        // console stood on a table the new room does not have: it goes on top of the SM_SoundBox by
        // the east wall (top at 0.84 m, 0.59 x 1.2 m), its 1.22 m width along the box, facing the room.
        private static readonly Dictionary<string, (string entry, Vector3 localOffset, Vector3 localRotation)> EntryPoses = new Dictionary<string, (string, Vector3, Vector3)>
        {
            { "MissionSoundManager", ("SoundManager", new Vector3(5.349f, 0.845f, 2.57f), new Vector3(0f, 270f, 0f)) },
        };

        // What the mission needs from its backup room and the new room lacks, brought along with the
        // same shift as the entries: the lights puzzle's switch table and the statue its lamps aim at.
        private static readonly Dictionary<string, string[]> CarryOver = new Dictionary<string, string[]>
        {
            { "MissionLightsRoom", new[] { "LampsPuzzle_Proto", "Monster_Low" } },
        };

        // Ceiling fixtures a room lamp goes under.
        private static readonly string[] Fixtures = { "SM_Hallway_Light_Roof", "SM_Light_Ceiling_Rectangular", "SM_Straight_Ceiling_Light", "SM_Single_Light" };
        private const string PendantPath = "Assets/Prefabs/Rooms/Props/Hallway/SM_Hallway_Light_Roof.prefab";
        private const string RoomLightPath = "Assets/Prefabs/Managers/SabotageObjects/SabotageLight.prefab";

        private static readonly string[] BaseRooms =
        {
            "Assets/Prefabs/Rooms/Fixed/BaseRoom_Decorated_1.prefab",
            "Assets/Prefabs/Rooms/Fixed/BaseRoom_Decorated_2.prefab",
            "Assets/Prefabs/Rooms/Fixed/BaseRoom_Decorated_3.prefab",
            "Assets/Prefabs/Rooms/Fixed/BaseRoom_Decorated_4.prefab",
            "Assets/Prefabs/Rooms/Fixed/BaseRoom_Decorated_5.prefab",
        };

        // Mission room name in the Hamlet contract → the new 14.7 m room it moves into.
        private static readonly Dictionary<string, string> MissionRooms = new Dictionary<string, string>
        {
            { "MissionLightsRoom", "Assets/Prefabs/Rooms/Missions/MissionRoomLights.prefab" },
            { "MIssionBoxRoom", "Assets/Prefabs/Rooms/Missions/MissionRoomBox.prefab" },
            { "MissionPipesRoom", "Assets/Prefabs/Rooms/Missions/MissionRoomPipes.prefab" },
            { "MissionSoundManager", "Assets/Prefabs/Rooms/Missions/MissionRoomSound.prefab" },
        };

        private static readonly string[] AudienceProps =
        {
            "Assets/Prefabs/Audience_Objects/SM_Crown.prefab",
            "Assets/Prefabs/Audience_Objects/Happy_Mask_Low.prefab",
            "Assets/Prefabs/Audience_Objects/Sad_Mask_Low.prefab",
            "Assets/Prefabs/Audience_Objects/Sword.prefab",
        };

        public static void EnsureFolders()
        {
            // Parents before children: each folder is created inside the one before it.
            foreach (string f in new[] { Root, RoomsFolder, DataFolder, ContractFolder, RoomDataFolder, BaseRoomDataFolder, MissionRoomDataFolder, MissionDataFolder, MaterialsFolder, Root + "/Lighting", "Assets/Scenes/Prototypes" })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                string parent = Path.GetDirectoryName(f).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(f));
            }
        }

        /// <summary>Builds (or rebuilds) the room variants, missions and contract. Returns the contract.</summary>
        public static ContractsSO EnsureAll(bool rebuildRooms)
        {
            EnsureFolders();

            var baseData = new List<RoomDataSO>();
            for (int i = 0; i < BaseRooms.Length; i++)
            {
                GameObject variant = RoomVariant(BaseRooms[i], i, rebuildRooms, mission: false);
                baseData.Add(RoomData("Proto_" + Path.GetFileNameWithoutExtension(BaseRooms[i]), variant, RoomType.Normal, null, 100 + i));
            }

            var source = AssetDatabase.LoadAssetAtPath<ContractsSO>(SourceContract);
            var copies = source.personalMissions.ToDictionary(m => m, m => MissionCopy(m));
            var missions = new List<MissionSO>();
            foreach (MissionSO m in source.personalMissions)
            {
                RoomDataSO room = null;
                if (m.requiredRoom != null && MissionRooms.TryGetValue(m.requiredRoom.name, out string newRoom))
                {
                    GameObject variant = RoomVariant(newRoom, missions.Count + 7, rebuildRooms, mission: true, m.requiredRoom);
                    room = RoomData("Proto_" + m.requiredRoom.name, variant, m.requiredRoom.roomType, m.requiredRoom, m.requiredRoom.roomID, copies);
                    room.isUniqueRoom = m.requiredRoom.isUniqueRoom;
                    EditorUtility.SetDirty(room);
                }
                MissionSO copy = copies[m];
                copy.requiredRoom = room != null ? room : m.requiredRoom;
                EditorUtility.SetDirty(copy);
                missions.Add(copy);
            }

            var contract = AssetDatabase.LoadAssetAtPath<ContractsSO>(ContractPath);
            if (contract == null)
            {
                contract = ScriptableObject.CreateInstance<ContractsSO>();
                AssetDatabase.CreateAsset(contract, ContractPath);
            }
            contract.contractName = source.contractName + " (protótipo)";
            contract.mainMission = source.mainMission;
            contract.mainMissionPrefab = source.mainMissionPrefab;
            contract.personalMissions = missions;
            contract.baseRooms = baseData;
            contract.lootRooms = new List<RoomDataSO>();
            contract.maxAudience = source.maxAudience;
            EditorUtility.SetDirty(contract);
            AssetDatabase.SaveAssets();
            return contract;
        }

        private static RoomDataSO RoomData(string name, GameObject prefab, RoomType type, RoomDataSO source, int id, Dictionary<MissionSO, MissionSO> missions = null)
        {
            // Mission rooms are the ones copied from a Hamlet room; the rest are base rooms.
            string path = (source != null ? MissionRoomDataFolder : BaseRoomDataFolder) + "/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<RoomDataSO>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RoomDataSO>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.roomID = id;
            data.prefab = prefab;
            data.roomType = type;
            data.isUniqueRoom = false;
            data.networkSpawnEntries = new List<NetworkSpawnEntry>();
            if (source != null && source.networkSpawnEntries != null)
            {
                RoomShifts.TryGetValue(source.name, out Vector3 roomShift);
                EntryFixes.TryGetValue(source.name, out var fixes);
                bool posed = EntryPoses.TryGetValue(source.name, out var pose);
                foreach (NetworkSpawnEntry e in source.networkSpawnEntries)
                {
                    GameObject entry = EntryVariant(e.prefab, fixes ?? new (string, Vector3)[0], missions);
                    var placed = new NetworkSpawnEntry { prefab = entry, localOffset = e.localOffset + MissionShift + roomShift, localRotation = e.localRotation };
                    if (posed && e.prefab != null && e.prefab.name == pose.entry)
                    {
                        placed.localOffset = pose.localOffset;
                        placed.localRotation = pose.localRotation;
                    }
                    data.networkSpawnEntries.Add(placed);
                }
            }
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Proto_ variant of an entry prefab with some of its spawn points moved, and pointing at the
        /// prototype copies of its missions. Rewritten every time, so the variant follows the original
        /// apart from those.
        ///
        /// <para>The copies matter: the HUD holds the missions the contract hands out and drops one
        /// when its puzzle reports it done, comparing the two MissionSOs as objects. A puzzle still
        /// naming the Hamlet original never matched the prototype copy, so finished missions stayed
        /// on the list.</para>
        /// </summary>
        private static GameObject EntryVariant(GameObject entry, (string point, Vector3 localPosition)[] fixes, Dictionary<MissionSO, MissionSO> missions)
        {
            if (entry == null) return null;

            string path = RoomsFolder + "/Proto_" + entry.name + ".prefab";
            UnityEngine.SceneManagement.Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(entry, preview);
                go.name = "Proto_" + entry.name;
                foreach ((string point, Vector3 localPosition) in fixes)
                {
                    Transform t = go.transform.Find(point);
                    if (t == null) { Debug.LogWarning($"Protótipo: {entry.name} não tem {point}."); continue; }
                    t.position = go.transform.TransformPoint(localPosition);
                }
                if (missions != null)
                {
                    foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour == null) continue;
                        var so = new SerializedObject(behaviour);
                        SerializedProperty p = so.GetIterator();
                        while (p.Next(true))
                        {
                            if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is MissionSO m && missions.TryGetValue(m, out MissionSO copy))
                                p.objectReferenceValue = copy;
                        }
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                return WithOwnNetworkId(PrefabUtility.SaveAsPrefabAsset(go, path));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static readonly MethodInfo NetworkObjectOnValidate =
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>
        /// A variant saved from an instance keeps the GlobalObjectIdHash of the prefab it came from,
        /// so Netcode finds two network prefabs with one id: the host drops the variant and the
        /// clients fail the NetworkConfig check. NetworkObject.OnValidate makes the variant's own id
        /// and saving writes it as an override.
        /// </summary>
        public static GameObject WithOwnNetworkId(GameObject prefab)
        {
            var networkObject = prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            if (networkObject == null || NetworkObjectOnValidate == null) return prefab;

            NetworkObjectOnValidate.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            PrefabUtility.SavePrefabAsset(prefab);
            return prefab;
        }

        // requiredRoom is set by EnsureAll once the mission's room exists.
        private static MissionSO MissionCopy(MissionSO m)
        {
            string path = MissionDataFolder + "/Proto_" + m.name + ".asset";
            var copy = AssetDatabase.LoadAssetAtPath<MissionSO>(path);
            if (copy == null)
            {
                copy = ScriptableObject.CreateInstance<MissionSO>();
                AssetDatabase.CreateAsset(copy, path);
            }
            copy.missionID = m.missionID;
            copy.missionName = m.missionName;
            copy.instructions = m.instructions;
            copy.missionImage = m.missionImage;
            copy.objectiveType = m.objectiveType;
            EditorUtility.SetDirty(copy);
            return copy;
        }

        // ------------------------------------------------------------------ rooms

        /// <summary>
        /// A variant of a 14.7 m room that works in a match: floor on Ground and wall colliders on
        /// Walls (the monster's NavMesh and the player's step check use those layers), lamps under
        /// the ceiling fixtures, and something worth filming. A mission room also gets what its
        /// mission needs from the backup room (<see cref="CarryOver"/>).
        /// </summary>
        public static GameObject RoomVariant(string sourcePath, int index, bool rebuild, bool mission, RoomDataSO backup = null)
        {
            string path = RoomsFolder + "/Proto_" + Path.GetFileNameWithoutExtension(sourcePath) + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild) return existing;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            UnityEngine.SceneManagement.Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                go.name = "Proto_" + source.name;
                FixRoom(go, index, mission);
                if (backup != null) BringFromBackup(go, backup);
                var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
                return WithOwnNetworkId(saved);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void FixRoom(GameObject room, int index, bool mission)
        {
            // The rooms come without wall colliders. Walls sit under BaseStructure/Walls, or straight
            // under the root in some rooms (BaseRoom_Decorated_5).
            var wallGroups = new List<Transform>();
            Bounds? floorBounds = null;
            Transform structure = room.transform.Find("BaseStructure");
            if (structure != null)
            {
                Transform floor = structure.Find("Floor"), walls = structure.Find("Walls"), roof = structure.Find("Roof");
                if (floor != null)
                {
                    PrototypeGeometry.SetLayerRecursive(floor.gameObject, PrototypeGeometry.Ground);
                    foreach (Renderer r in floor.GetComponentsInChildren<Renderer>(true))
                    {
                        if (floorBounds == null) floorBounds = r.bounds;
                        else { Bounds b = floorBounds.Value; b.Encapsulate(r.bounds); floorBounds = b; }
                    }
                }
                if (roof != null) PrototypeGeometry.SetLayerRecursive(roof.gameObject, PrototypeGeometry.Scenary);
                if (walls != null) wallGroups.Add(walls);
            }
            foreach (Transform child in room.transform)
            {
                if (child.name.StartsWith("SM_Wall") || child.name.StartsWith("SM_Sophisticated_Wall")) wallGroups.Add(child);
            }
            foreach (Transform walls in wallGroups)
            {
                PrototypeGeometry.SetLayerRecursive(walls.gameObject, PrototypeGeometry.Walls);
                foreach (MeshFilter mf in walls.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
            }

            // The pipes room carries wall pipes laid out for some other placement of the puzzle: next
            // to the grid MissionShift puts here they look like an inlet and outlet the solved pipes
            // never reach.
            if (mission)
            {
                foreach (Transform child in room.transform)
                {
                    if (child.name == "PipeConnectionTriple" || child.name.StartsWith("SM_Pipe_L")) child.gameObject.SetActive(false);
                }
            }

            // The floor collider: on Ground, the size of the floor tiles (it was 18.8 m on Default).
            Vector3 floorCenter = floorBounds?.center ?? room.transform.TransformPoint(new Vector3(-1f, 0f, -1f));
            Vector3 floorSize = floorBounds.HasValue ? new Vector3(floorBounds.Value.size.x, 0f, floorBounds.Value.size.z) : new Vector3(14.7f, 0f, 14.7f);
            foreach (BoxCollider bc in room.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Floor"))
            {
                bc.gameObject.layer = PrototypeGeometry.Ground;
                bc.center = bc.transform.InverseTransformPoint(floorCenter + Vector3.down * 0.1f);
                bc.size = new Vector3(floorSize.x, 0.2f, floorSize.z);
            }

            // Lamps, warm and low, each under a ceiling fixture the room shows lit. The rooms carry no
            // Light of their own, and some no fixture either (or only switched-off ones): those get
            // four hanging lamps. SabotageLight, like the corridor sconces, so the monster can put
            // them out. The lights puzzle room stays dimmer, for its spotlights to read.
            var fixtures = room.GetComponentsInChildren<MeshRenderer>(false).Where(r => Fixtures.Any(f => r.name.StartsWith(f))).ToList();
            if (fixtures.Count == 0)
            {
                Transform hanging = new GameObject("Luminarias").transform;
                hanging.SetParent(room.transform, false);
                foreach (Vector3 corner in new[] { new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, -1f) })
                {
                    var pendant = PrototypeGeometry.Place(PendantPath, hanging, floorCenter + corner * 3.6f + Vector3.up * 3f, Quaternion.identity);
                    if (pendant != null) fixtures.AddRange(pendant.GetComponentsInChildren<MeshRenderer>());
                }
            }
            var palette = new[] { new Color(1f, 0.78f, 0.52f), new Color(1f, 0.72f, 0.45f), new Color(0.95f, 0.82f, 0.62f) };
            float dim = room.name.Contains("Lights") ? 0.6f : 1f;
            int n = 0;
            foreach (MeshRenderer fixture in fixtures)
            {
                Bounds b = fixture.bounds;
                var light = PrototypeGeometry.Place(RoomLightPath, fixture.transform, new Vector3(b.center.x, b.min.y - 0.12f, b.center.z), Quaternion.identity);
                if (light == null) continue;
                var l = light.GetComponent<Light>();
                l.type = LightType.Point;
                l.color = palette[(index + n) % palette.Length];
                l.intensity = (fixtures.Count > 4 ? 10f : 14f) * dim;
                l.range = fixtures.Count > 4 ? 10f : 12f;
                l.shadows = n == 0 ? LightShadows.Soft : LightShadows.None; // one lamp per room casts shadows
                l.lightmapBakeType = LightmapBakeType.Realtime;
                n++;
            }

            // Something to film: on top of a piece of furniture, never in a mission spot.
            Transform audience = room.transform.Find("AudienceObjects");
            if (audience != null && audience.childCount == 0)
            {
                Physics.SyncTransforms();
                var surfaces = room.GetComponentsInChildren<BoxCollider>(true)
                    .Where(c => c.gameObject.layer == PrototypeGeometry.Scenary && c.bounds.max.y > 0.45f && c.bounds.max.y < 1.6f && c.bounds.size.x > 0.4f && c.bounds.size.z > 0.4f)
                    .OrderBy(c => c.name).ToList();
                if (!mission && surfaces.Count > 0)
                {
                    BoxCollider top = surfaces[(index * 7) % surfaces.Count];
                    Vector3 at = new Vector3(top.bounds.center.x, top.bounds.max.y, top.bounds.center.z);
                    var prop = PrototypeGeometry.Place(AudienceProps[index % AudienceProps.Length], audience, at, Quaternion.Euler(0f, index * 53f, 0f));
                    if (prop != null && AudienceProps[index % AudienceProps.Length].Contains("Sword")) prop.transform.rotation = Quaternion.Euler(0f, index * 53f, 75f);
                }
            }
        }

        /// <summary>
        /// Copies <see cref="CarryOver"/>'s pieces of the backup room into the new one, where the
        /// entries land: same spot relative to the room after MissionShift and the room's own shift.
        /// </summary>
        private static void BringFromBackup(GameObject room, RoomDataSO backup)
        {
            if (!CarryOver.TryGetValue(backup.name, out string[] names) || backup.prefab == null) return;

            RoomShifts.TryGetValue(backup.name, out Vector3 roomShift);
            foreach (string name in names)
            {
                Transform source = backup.prefab.transform.Find(name);
                if (source == null) { Debug.LogWarning($"Protótipo: {backup.prefab.name} não tem {name}."); continue; }

                var copy = Object.Instantiate(source.gameObject, room.transform);
                copy.name = name;
                copy.transform.localPosition = source.localPosition + MissionShift + roomShift;
                copy.transform.localRotation = source.localRotation;
                copy.transform.localScale = source.localScale;

                // The statue has no collider of its own in the backup room either: players walked into it.
                var skinned = copy.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinned != null && copy.GetComponentInChildren<Collider>() == null)
                {
                    PrototypeGeometry.SetLayerRecursive(copy, PrototypeGeometry.Scenary);
                    var capsule = copy.AddComponent<CapsuleCollider>();
                    Vector3 c = copy.transform.InverseTransformPoint(skinned.bounds.center);
                    float height = skinned.bounds.size.y / copy.transform.lossyScale.y;
                    capsule.center = new Vector3(c.x, height * 0.5f, c.z);
                    capsule.height = height;
                    capsule.radius = 0.35f;
                }
            }
        }
    }
}
