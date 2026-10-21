using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LevelDesign.Toolkit
{
    /// <summary>
    /// Renders each prefab once in an isolated preview scene and keeps the picture in
    /// Library/LevelDesignToolkit. Bigger and better lit than AssetPreview, and rooms come out as a
    /// cutaway (the roof is left out) so you can read the layout.
    /// </summary>
    public static class ToolkitThumbnails
    {
        public const int Size = 256;

        private static readonly Dictionary<string, Texture2D> Memory = new();
        private static string CacheDirectory => Path.Combine(LevelToolkitScanner.ProjectRoot, "Library", "LevelDesignToolkit", "thumbs");

        public static Texture2D Cached(ToolkitEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.guid)) return null;
            if (Memory.TryGetValue(entry.guid, out var texture) && texture != null) return texture;

            string file = Path.Combine(CacheDirectory, entry.guid + ".png");
            if (!File.Exists(file)) return null;

            texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.LoadImage(File.ReadAllBytes(file));
            Memory[entry.guid] = texture;
            return texture;
        }

        public static Texture2D Get(ToolkitEntry entry, bool force = false)
        {
            if (!force)
            {
                var cached = Cached(entry);
                if (cached != null) return cached;
            }

            if (entry?.prefab == null) return null;

            var texture = Render(entry.prefab, entry.category);
            if (texture == null) return null;

            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllBytes(Path.Combine(CacheDirectory, entry.guid + ".png"), texture.EncodeToPNG());

            if (Memory.TryGetValue(entry.guid, out var old) && old != null && old != texture) Object.DestroyImmediate(old);
            Memory[entry.guid] = texture;
            return texture;
        }

        public static void ClearCache()
        {
            foreach (var texture in Memory.Values.Where(t => t != null)) Object.DestroyImmediate(texture);
            Memory.Clear();

            if (Directory.Exists(CacheDirectory)) Directory.Delete(CacheDirectory, true);
        }

        private static Texture2D Render(GameObject prefab, ToolkitCategory category)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();

            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (instance == null) return null;

                bool isRoom = category is ToolkitCategory.SAL_SalaPronta or ToolkitCategory.COR_ModuloCorredor;
                var bounds = VisibleBounds(instance, isRoom);
                if (bounds.size == Vector3.zero) return null;

                var camera = Create<Camera>(scene, "ThumbCamera");
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.16f, 0.19f, 1f);
                camera.fieldOfView = 28f;
                camera.cameraType = CameraType.Preview;

                AddLight(scene, Quaternion.Euler(45f, -35f, 0f), 1.5f);
                AddLight(scene, Quaternion.Euler(20f, 150f, 0f), 0.7f);
                AddLight(scene, Quaternion.Euler(-60f, 0f, 0f), 0.35f);

                // Ceilings are single-sided and face down: look at them from below.
                float pitch = category == ToolkitCategory.TET_Teto ? -40f : isRoom ? 58f : 24f;
                var direction = Quaternion.Euler(pitch, 145f, 0f) * Vector3.forward;
                float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
                float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

                camera.transform.SetPositionAndRotation(bounds.center - direction * distance, Quaternion.LookRotation(direction));
                camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
                camera.farClipPlane = distance + radius * 2f;

                // Rendered at twice the size and scaled down instead of MSAA: a multisampled target
                // through a render request makes URP log render-pass errors for every picture.
                var target = new RenderTexture(Size * 2, Size * 2, 24, RenderTextureFormat.ARGB32);
                var small = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                camera.targetTexture = target;

                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();

                Graphics.Blit(target, small);
                RenderTexture.active = small;
                var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;

                camera.targetTexture = null;
                target.Release();
                small.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(small);
                return texture;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>Rooms lose everything above head height, so the picture shows the floor plan.</summary>
        private static Bounds VisibleBounds(GameObject instance, bool cutaway)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is MeshRenderer or SkinnedMeshRenderer)
                .ToArray();

            if (renderers.Length == 0) return new Bounds();

            var all = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) all.Encapsulate(renderer.bounds);

            if (cutaway && all.size.y < 6f)
            {
                float cut = all.min.y + 2.4f;
                foreach (var renderer in renderers.Where(r => r.bounds.min.y > cut)) renderer.enabled = false;
            }

            return all;
        }

        private static T Create<T>(Scene scene, string name) where T : Component
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            var component = go.AddComponent<T>();
            if (component is Camera camera) camera.scene = scene;
            return component;
        }

        private static void AddLight(Scene scene, Quaternion rotation, float intensity)
        {
            var light = Create<Light>(scene, "ThumbLight");
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.transform.rotation = rotation;
        }
    }
}
