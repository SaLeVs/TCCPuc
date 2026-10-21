using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// Mood and light for the prototype levels. Lamps stay real-time (the monster switches them off)
    /// with their bounce baked into lightmaps and light probes, so rooms read as lit spaces and the
    /// characters pick up the colour of the corridor they walk through.
    /// </summary>
    public static class PrototypeLighting
    {
        public const string SettingsPath = PrototypeAssets.Root + "/Lighting/Prototipo.lighting";
        public const string VolumePath = PrototypeAssets.Root + "/Lighting/Prototipo_Volume.asset";

        public static void Setup(PrototypeBuilder.Context ctx)
        {
            // Ambient: almost dark, a cold top and a warm horizon, light fog to give depth to long corridors.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.075f, 0.08f, 0.095f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.058f, 0.05f);
            RenderSettings.ambientGroundColor = new Color(0.015f, 0.014f, 0.013f);
            RenderSettings.reflectionIntensity = 0.55f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.03f, 0.028f, 0.03f);
            RenderSettings.skybox = null;

            Lightmapping.lightingSettings = Settings();

            // Post-processing: the game's profile, a little darker at the edges and grainier.
            var volume = ctx.Find("Global Volume")?.GetComponent<Volume>();
            if (volume != null) volume.sharedProfile = Profile(volume.sharedProfile);

            // Light probes along every corridor (knee and head height) and through the hub rooms.
            var group = Group("LightProbes", ctx.Root);
            var probes = group.AddComponent<LightProbeGroup>();
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (Vector3 p in ctx.Corridors.LightPoints) { pts.Add(p + Vector3.up * 0.6f); pts.Add(p + Vector3.up * 2.2f); }
            foreach (var s in ctx.Corridors.Stairs) { pts.Add((s.Top + s.Bottom) * 0.5f + Vector3.up * 1.2f); }
            foreach (WRoom r in ctx.Layout.Rooms)
            {
                Rect f = r.Footprint;
                for (float x = f.xMin + 3f; x < f.xMax - 2f; x += 6f)
                for (float z = f.yMin + 3f; z < f.yMax - 2f; z += 6f)
                {
                    float y = r.FloorY;
                    if (r.Room.Kind == RoomKind.Auditorium) y = Mathf.Clamp((z - (r.Pivot.z - 6f)) / 22f, 0f, 1f) * 4.5f;
                    pts.Add(new Vector3(x, y + 0.8f, z));
                    pts.Add(new Vector3(x, y + 2.4f, z));
                }
            }
            probes.probePositions = pts.Select(p => group.transform.InverseTransformPoint(p)).ToArray();

            // Reflection probes: one per corridor lamp cluster and one per hub room, baked with the level.
            var refl = Group("ReflectionProbes", ctx.Root).transform;
            int n = 0;
            foreach (Vector3 p in ctx.Corridors.LightPoints.Where((_, i) => i % 3 == 0))
            {
                var go = new GameObject("Reflexo");
                go.transform.SetParent(refl, false);
                go.transform.position = p + Vector3.up * 1.6f;
                var rp = go.AddComponent<ReflectionProbe>();
                rp.mode = ReflectionProbeMode.Baked;
                rp.size = new Vector3(14f, WallHeight + 0.5f, 14f);
                rp.boxProjection = true;
                rp.resolution = 128;
                rp.intensity = 0.9f;
                n++;
            }
            foreach (WRoom r in ctx.Layout.Rooms.Where(r => r.Room.Kind != RoomKind.Puzzle))
            {
                var go = new GameObject("Reflexo_" + r.Room.Kind);
                go.transform.SetParent(refl, false);
                go.transform.position = new Vector3(r.Footprint.center.x, r.FloorY + 1.8f, r.Footprint.center.y);
                var rp = go.AddComponent<ReflectionProbe>();
                rp.mode = ReflectionProbeMode.Baked;
                rp.size = new Vector3(r.Footprint.width, r.Room.Kind == RoomKind.Auditorium ? 22f : 4.5f, r.Footprint.height);
                rp.boxProjection = true;
                rp.resolution = 256;
                n++;
            }
            ctx.Report.AppendLine($"  luz: {pts.Count} light probes, {n} reflection probes, névoa e volume do protótipo (falta assar: Tools/TCC/Protótipos/Assar luz e NavMesh)");
        }

        private static LightingSettings Settings()
        {
            var s = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (s != null) return s;
            s = new LightingSettings { name = "Prototipo" };
            s.bakedGI = true;
            s.realtimeGI = false;
            s.mixedBakeMode = MixedLightingMode.IndirectOnly;
            s.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            s.lightmapResolution = 8f;
            s.lightmapPadding = 2;
            s.lightmapMaxSize = 2048;
            s.directionalityMode = LightmapsMode.NonDirectional;
            s.indirectScale = 1.2f;
            s.albedoBoost = 1.1f;
            s.directSampleCount = 16;
            s.indirectSampleCount = 256;
            s.environmentSampleCount = 64;
            s.maxBounces = 2;
            s.filteringMode = LightingSettings.FilterMode.Auto;
            AssetDatabase.CreateAsset(s, SettingsPath);
            return s;
        }

        private static VolumeProfile Profile(VolumeProfile source)
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (p != null) return p;
            if (source == null) return null;
            p = Object.Instantiate(source);
            p.name = "Prototipo_Volume";
            AssetDatabase.CreateAsset(p, VolumePath);
            foreach (var comp in source.components)
            {
                var copy = Object.Instantiate(comp);
                copy.name = comp.name;
                AssetDatabase.AddObjectToAsset(copy, p);
            }
            p.components = p.components.Select(c => AssetDatabase.LoadAllAssetsAtPath(VolumePath).OfType<VolumeComponent>().FirstOrDefault(x => x.GetType() == c.GetType()) ?? c).ToList();

            if (p.TryGet(out UnityEngine.Rendering.Universal.Vignette vignette)) { vignette.intensity.Override(0.38f); vignette.smoothness.Override(0.45f); }
            if (p.TryGet(out UnityEngine.Rendering.Universal.FilmGrain grain)) { grain.intensity.Override(0.28f); }
            if (p.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom)) { bloom.threshold.Override(0.95f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.72f); }
            if (p.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments color)) { color.contrast.Override(12f); color.saturation.Override(-12f); color.postExposure.Override(0.15f); }
            if (p.TryGet(out UnityEngine.Rendering.Universal.Tonemapping tone)) { tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES); }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // ------------------------------------------------------------------ bake

        [MenuItem("Tools/TCC/Protótipos/Assar luz e NavMesh do nível aberto", priority = 40)]
        public static void BakeOpen()
        {
            Scene s = SceneManager.GetActiveScene();
            if (!s.path.StartsWith(PrototypeBuilder.SceneFolder)) { Debug.LogWarning("Abra (sozinha) uma cena de protótipo primeiro."); return; }
            if (SceneManager.sceneCount > 1) { Debug.LogWarning("Deixe só a cena do protótipo aberta: o NavMesh e a luz juntam tudo que estiver carregado."); return; }
            Bake(s);
        }

        [MenuItem("Tools/TCC/Protótipos/Assar só o NavMesh do nível aberto", priority = 41)]
        public static void BakeNavMeshOpen()
        {
            Scene s = SceneManager.GetActiveScene();
            if (!s.path.StartsWith(PrototypeBuilder.SceneFolder)) { Debug.LogWarning("Abra (sozinha) uma cena de protótipo primeiro."); return; }
            if (SceneManager.sceneCount > 1) { Debug.LogWarning("Deixe só a cena do protótipo aberta: o NavMesh junta tudo que estiver carregado."); return; }
            Debug.Log(BakeNavMesh(s));
            EditorSceneManager.SaveScene(s);
        }

        public static string Bake(Scene s)
        {
            string nav = BakeNavMesh(s);
            Lightmapping.Bake();
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            return nav + "; luz assada: " + s.path;
        }

        /// <summary>
        /// Builds the NavMesh and keeps it as an asset next to the scene (GameN/NavMesh-NavMesh.asset),
        /// the way the NavMeshSurface's own Bake button does. Built from code it only lives in memory,
        /// and the scene would save a reference to nothing. Only the scene being baked may be loaded:
        /// the surface collects everything that is.
        /// </summary>
        public static string BakeNavMesh(Scene s)
        {
            var nav = s.GetRootGameObjects().Select(g => g.GetComponentInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).FirstOrDefault(n => n != null);
            if (nav == null) return "sem NavMeshSurface em " + s.path;
            nav.BuildNavMesh();

            string parent = System.IO.Path.GetDirectoryName(s.path).Replace('\\', '/');
            string folder = parent + "/" + s.name;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, s.name);
            string asset = folder + "/NavMesh-" + nav.gameObject.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(asset) != null) AssetDatabase.DeleteAsset(asset);
            AssetDatabase.CreateAsset(nav.navMeshData, asset);
            EditorSceneManager.MarkSceneDirty(s);
            var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            return $"NavMesh assado: {tri.indices.Length / 3} triângulos em {asset}";
        }
    }
}
