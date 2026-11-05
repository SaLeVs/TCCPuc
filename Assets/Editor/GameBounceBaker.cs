using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Bakes the bounced light of the Game scene into light probes.
///
/// <para>The level is about 9000 modular pieces lit in realtime, so a normal bake only saw the small
/// props marked static and no lamp light ever bounced off a wall. Marking everything for good would
/// write thousands of overrides into the scene, and nothing of that is needed once the probes exist.
/// So everything is set up on the open scene, baked, and thrown away by reopening it: the only things
/// left are the lighting data files. Lights stay Realtime and pieces stay non-static.</para>
///
/// <para>Use this instead of Generate Lighting, which bakes the scene as saved, without the bounce.</para>
/// </summary>
public static class GameBounceBaker
{
    private const string ScenePath = "Assets/Scenes/Game.unity";

    private const float ProbeSpacing = 2.5f;
    private static readonly float[] ProbeHeights = { 0.3f, 1.5f, 2.8f };

    // Spawned rooms do not exist at bake time. Open to the sky here, these probes hold about the
    // ambient light the rooms always had.
    private const string RoomSectorPrefix = "Sector_Room";
    private const float RoomHalfSize = 12f;
    private const float RoomProbeSpacing = 8f;
    private static readonly float[] RoomProbeHeights = { 0.5f, 3f };

    private const float MinContributorSize = 1f;
    // The walls are dark, so one physically correct bounce barely shows.
    private const float IndirectScale = 3f;

    [MenuItem("Tools/Iluminação/Bake da luz rebatida (Game)")]
    public static void Bake()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != ScenePath)
        {
            Debug.LogError($"[GameBounceBaker] Open {ScenePath} first.");
            return;
        }

        if (scene.isDirty)
        {
            Debug.LogError("[GameBounceBaker] Save the scene first: it is reopened from disk after the bake.");
            return;
        }

        LightingSettings settings = Lightmapping.lightingSettings;
        settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
        settings.indirectScale = IndirectScale;

        // Probes only need few rays, and on this machine a GPU bake has taken the whole system down.
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);

        int lights = PrepareLights();
        int contributors = PrepareRenderers();
        int probes = CreateProbes(scene);

        Debug.Log($"[GameBounceBaker] Baking {lights} lights, {contributors} contributing pieces, {probes} probes.");

        Lightmapping.bakeCompleted += OnBakeFinished;
        Lightmapping.bakeCancelled += OnBakeFinished;

        if (Lightmapping.BakeAsync()) return;

        Debug.LogError("[GameBounceBaker] The bake did not start.");
        OnBakeFinished();
    }

    private static int PrepareLights()
    {
        int count = 0;

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!light.enabled || light.type == LightType.Directional) continue;

            light.lightmapBakeType = LightmapBakeType.Mixed;

            // The lamp prefabs have an Indirect Multiplier of 0, meant for when nothing was baked.
            light.bounceIntensity = 1f;

            // Without shadows the bake would send their light through the walls.
            if (light.shadows == LightShadows.None) light.shadows = LightShadows.Soft;

            count++;
        }

        return count;
    }

    private static int PrepareRenderers()
    {
        int count = 0;

        foreach (MeshRenderer meshRenderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            GameObject owner = meshRenderer.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(owner);

            meshRenderer.receiveGI = ReceiveGI.LightProbes;

            bool contributes = meshRenderer.enabled && meshRenderer.bounds.size.magnitude >= MinContributorSize;
            flags = contributes ? flags | StaticEditorFlags.ContributeGI : flags & ~StaticEditorFlags.ContributeGI;
            GameObjectUtility.SetStaticEditorFlags(owner, flags);

            if (contributes) count++;
        }

        return count;
    }

    private static int CreateProbes(Scene scene)
    {
        var positions = new List<Vector3>();
        var taken = new HashSet<(int, int, int)>();

        NavMeshTriangulation navMesh = NavMesh.CalculateTriangulation();
        Physics.SyncTransforms();

        for (int i = 0; i < navMesh.indices.Length; i += 3)
        {
            AddProbesOverTriangle(
                navMesh.vertices[navMesh.indices[i]],
                navMesh.vertices[navMesh.indices[i + 1]],
                navMesh.vertices[navMesh.indices[i + 2]],
                positions,
                taken);
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Sectors") continue;

            foreach (Transform sector in root.transform)
            {
                if (sector.name.StartsWith(RoomSectorPrefix)) AddRoomProbes(sector.position, positions);
            }
        }

        var holder = new GameObject("Bounce Probes (bake only)");
        SceneManager.MoveGameObjectToScene(holder, scene);
        holder.AddComponent<LightProbeGroup>().probePositions = positions.ToArray();

        return positions.Count;
    }

    private static void AddProbesOverTriangle(Vector3 a, Vector3 b, Vector3 c, List<Vector3> positions, HashSet<(int, int, int)> taken)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / ProbeSpacing);
        int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x) / ProbeSpacing);
        int minZ = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / ProbeSpacing);
        int maxZ = Mathf.CeilToInt(Mathf.Max(a.z, b.z, c.z) / ProbeSpacing);

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                var point = new Vector2(x * ProbeSpacing, z * ProbeSpacing);
                if (!TryHeightOnTriangle(point, a, b, c, out float floor)) continue;
                if (!taken.Add((x, z, Mathf.RoundToInt(floor)))) continue;

                var ground = new Vector3(point.x, floor, point.y);

                foreach (float height in ProbeHeights)
                {
                    // Under a low ceiling the upper probes would end up inside it.
                    if (Physics.Raycast(ground + Vector3.up * 0.1f, Vector3.up, height)) break;

                    positions.Add(ground + Vector3.up * height);
                }
            }
        }
    }

    private static bool TryHeightOnTriangle(Vector2 point, Vector3 a, Vector3 b, Vector3 c, out float height)
    {
        height = 0f;

        float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
        if (Mathf.Approximately(denominator, 0f)) return false;

        float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.y - c.z)) / denominator;
        float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.y - c.z)) / denominator;
        float w = 1f - u - v;

        if (u < 0f || v < 0f || w < 0f) return false;

        height = u * a.y + v * b.y + w * c.y;
        return true;
    }

    private static void AddRoomProbes(Vector3 center, List<Vector3> positions)
    {
        for (float x = -RoomHalfSize; x <= RoomHalfSize; x += RoomProbeSpacing)
        {
            for (float z = -RoomHalfSize; z <= RoomHalfSize; z += RoomProbeSpacing)
            {
                foreach (float height in RoomProbeHeights)
                {
                    positions.Add(center + new Vector3(x, height, z));
                }
            }
        }
    }

    private static void OnBakeFinished()
    {
        Lightmapping.bakeCompleted -= OnBakeFinished;
        Lightmapping.bakeCancelled -= OnBakeFinished;

        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.OpenScene(ScenePath);
            int probes = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0;
            Debug.Log($"[GameBounceBaker] Finished. Probes in the lighting data: {probes}.");
        };
    }
}
