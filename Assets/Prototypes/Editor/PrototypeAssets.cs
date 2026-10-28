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
        public const string MaterialsFolder = Root + "/Materials";

        private const string SourceContract = "Assets/Scriptable_objects/Contracts/Contract Hamlet.asset";
        public const string ContractPath = DataFolder + "/Contract Hamlet Prototipo.asset";

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
            foreach (string f in new[] { Root, RoomsFolder, DataFolder, MaterialsFolder, Root + "/Lighting", "Assets/Scenes/Prototypes" })
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
            var missions = new List<MissionSO>();
            foreach (MissionSO m in source.personalMissions)
            {
                RoomDataSO room = null;
                if (m.requiredRoom != null && MissionRooms.TryGetValue(m.requiredRoom.name, out string newRoom))
                {
                    GameObject variant = RoomVariant(newRoom, missions.Count + 7, rebuildRooms, mission: true);
                    room = RoomData("Proto_" + m.requiredRoom.name, variant, m.requiredRoom.roomType, m.requiredRoom, m.requiredRoom.roomID);
                    room.isUniqueRoom = m.requiredRoom.isUniqueRoom;
                    EditorUtility.SetDirty(room);
                }
                missions.Add(MissionCopy(m, room));
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

        private static RoomDataSO RoomData(string name, GameObject prefab, RoomType type, RoomDataSO source, int id)
        {
            string path = DataFolder + "/" + name + ".asset";
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
                foreach (NetworkSpawnEntry e in source.networkSpawnEntries)
                {
                    GameObject entry = EntryFixes.TryGetValue(source.name, out var fixes) ? EntryVariant(e.prefab, fixes) : e.prefab;
                    data.networkSpawnEntries.Add(new NetworkSpawnEntry { prefab = entry, localOffset = e.localOffset + MissionShift + roomShift, localRotation = e.localRotation });
                }
            }
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Proto_ variant of an entry prefab with some of its spawn points moved. Rewritten every time,
        /// so the variant follows the original apart from those points.
        /// </summary>
        private static GameObject EntryVariant(GameObject entry, (string point, Vector3 localPosition)[] fixes)
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

        private static MissionSO MissionCopy(MissionSO m, RoomDataSO room)
        {
            string path = DataFolder + "/Proto_" + m.name + ".asset";
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
            copy.requiredRoom = room != null ? room : m.requiredRoom;
            EditorUtility.SetDirty(copy);
            return copy;
        }

        // ------------------------------------------------------------------ rooms

        /// <summary>
        /// A variant of a 14.7 m room that works in a match: floor on Ground and wall colliders on
        /// Walls (the monster's NavMesh and the player's step check use those layers), lights at
        /// the wall sconces, and something worth filming.
        /// </summary>
        public static GameObject RoomVariant(string sourcePath, int index, bool rebuild, bool mission)
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
            Transform structure = room.transform.Find("BaseStructure");
            if (structure != null)
            {
                Transform floor = structure.Find("Floor"), walls = structure.Find("Walls"), roof = structure.Find("Roof");
                if (floor != null) PrototypeGeometry.SetLayerRecursive(floor.gameObject, PrototypeGeometry.Ground);
                if (roof != null) PrototypeGeometry.SetLayerRecursive(roof.gameObject, PrototypeGeometry.Scenary);
                if (walls != null)
                {
                    PrototypeGeometry.SetLayerRecursive(walls.gameObject, PrototypeGeometry.Walls);
                    foreach (MeshFilter mf in walls.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    }
                }
            }

            // The floor collider: on Ground, the size of the room (it was 18.8 m on Default).
            foreach (BoxCollider bc in room.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Floor"))
            {
                bc.gameObject.layer = PrototypeGeometry.Ground;
                bc.center = bc.transform.InverseTransformPoint(room.transform.TransformPoint(new Vector3(-1f, -0.1f, -1f)));
                bc.size = new Vector3(14.7f, 0.2f, 14.7f);
            }

            // Lights at the sconces, warm and low: each room is lit by its own lamps.
            var palette = new[] { new Color(1f, 0.78f, 0.52f), new Color(1f, 0.72f, 0.45f), new Color(0.95f, 0.82f, 0.62f) };
            int n = 0;
            foreach (Transform sconce in room.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("SM_Single_Light")).ToList())
            {
                PrototypeGeometry.SetLayerRecursive(sconce.gameObject, PrototypeGeometry.Props);
                var light = PrototypeGeometry.Place("Assets/Prefabs/Managers/SabotageObjects/SabotageLight.prefab", sconce, sconce.position + sconce.TransformDirection(Vector3.left) * 0.35f + Vector3.down * 0.1f, Quaternion.identity);
                if (light == null) continue;
                var l = light.GetComponent<Light>();
                l.type = LightType.Point;
                l.color = palette[(index + n) % palette.Length];
                l.intensity = 2.4f;
                l.range = 7.5f;
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
    }
}
