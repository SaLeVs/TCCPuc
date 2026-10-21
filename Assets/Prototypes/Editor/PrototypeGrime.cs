using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Prototypes.Editor
{
    /// <summary>
    /// The project has no decals, so the abandoned look gets its own: water stains and puddles on the
    /// floors, damp running down the walls, dust. Textures are generated once (noise) and used on
    /// transparent quads laid just off the surface.
    /// </summary>
    public static class PrototypeGrime
    {
        public enum Kind { Mancha, Poca, Escorrido, Poeira }

        private static readonly string TexFolder = PrototypeAssets.Root + "/Textures";
        private static readonly string MeshPath = PrototypeAssets.Root + "/Textures/Decal_Quad.asset";

        public static Material Get(Kind kind)
        {
            string name = "Grime_" + kind;
            string matPath = PrototypeAssets.MaterialsFolder + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m != null) return m;

            if (!AssetDatabase.IsValidFolder(TexFolder)) AssetDatabase.CreateFolder(PrototypeAssets.Root, "Textures");
            string texPath = TexFolder + "/" + name + ".png";
            if (!File.Exists(texPath))
            {
                bool tall = kind == Kind.Escorrido;
                int w = tall ? 128 : 256, h = tall ? 512 : 256;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(Generate(kind, w, h));
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }

            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + name };
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", kind == Kind.Poca ? 0.85f : kind == Kind.Escorrido ? 0.3f : 0.08f);
            // Transparent, alpha blended, no depth write.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f); // otherwise the whole quad keeps its highlight where alpha is 0
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = (int)RenderQueue.Transparent - 50;
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }

        /// <summary>A 1 x 1 quad on the XZ plane facing +y, UVs 0..1; scaled per decal.</summary>
        public static Mesh Quad()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh != null) return mesh;
            if (!AssetDatabase.IsValidFolder(TexFolder)) AssetDatabase.CreateFolder(PrototypeAssets.Root, "Textures");
            mesh = new Mesh { name = "Decal_Quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        /// <summary>A decal on a floor (normal up) or a wall (normal = inward), width x height metres.</summary>
        public static GameObject Place(Kind kind, Transform parent, Vector3 center, Vector3 normal, float width, float height, float spin)
        {
            var go = new GameObject("Mancha_" + kind);
            go.transform.SetParent(parent, false);
            go.layer = PrototypeGeometry.Props;
            // The quad faces local +y: point it along the normal; on a wall keep its "up" (texture v) upwards.
            Quaternion rot = Mathf.Abs(normal.y) > 0.9f ? Quaternion.Euler(0f, spin, 0f) : Quaternion.LookRotation(Vector3.up, normal);
            go.transform.SetPositionAndRotation(center + normal * 0.006f, rot);
            go.transform.localScale = new Vector3(width, 1f, height);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Get(kind);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ------------------------------------------------------------------ noise

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        private static float Noise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float x, float y, int seed, int octaves = 5)
        {
            float sum = 0, amp = 0.5f, f = 1f, norm = 0;
            for (int i = 0; i < octaves; i++) { sum += Noise(x * f, y * f, seed + i * 17) * amp; norm += amp; amp *= 0.5f; f *= 2.03f; }
            return sum / norm;
        }

        private static Color32[] Generate(Kind kind, int w, int h)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1), v = y / (float)(h - 1);
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0 centre, 1 edge
                Color c; float a;
                switch (kind)
                {
                    case Kind.Mancha:
                    {
                        // A dried water stain: blotchy, with a darker rim where the water stopped.
                        float n = Fbm(u * 4f, v * 4f, 11);
                        float field = n - r * 0.55f;
                        float inside = Mathf.SmoothStep(0.05f, 0.16f, field);
                        float rim = Mathf.Exp(-Mathf.Pow((field - 0.1f) / 0.03f, 2f));
                        a = Mathf.Clamp01(inside * 0.45f + rim * 0.5f) * (0.75f + 0.25f * Fbm(u * 16f, v * 16f, 5));
                        c = Color.Lerp(new Color(0.16f, 0.11f, 0.07f), new Color(0.07f, 0.05f, 0.03f), rim);
                        break;
                    }
                    case Kind.Poca:
                    {
                        float n = Fbm(u * 3f, v * 3f, 23);
                        float field = n - r * 0.6f;
                        a = Mathf.SmoothStep(0.02f, 0.12f, field) * 0.8f;
                        c = new Color(0.04f, 0.04f, 0.045f);
                        break;
                    }
                    case Kind.Escorrido:
                    {
                        // Damp running down from the top: streaks per column, fading towards the bottom.
                        float col = Fbm(u * 9f, 0.5f, 31, 3);
                        float len = 0.35f + col * 0.75f;
                        float fromTop = 1f - v;
                        float streak = Mathf.SmoothStep(0.42f, 0.62f, col) * Mathf.Clamp01(1f - fromTop / len);
                        float topBand = Mathf.Clamp01(1f - fromTop * 6f) * 0.6f;
                        float edge = Mathf.Clamp01(1f - Mathf.Abs(dx) * 2.2f);
                        a = Mathf.Clamp01((streak + topBand) * edge * (0.6f + 0.4f * Fbm(u * 20f, v * 40f, 7)));
                        c = Color.Lerp(new Color(0.12f, 0.11f, 0.06f), new Color(0.05f, 0.06f, 0.04f), fromTop);
                        break;
                    }
                    default:
                    {
                        float n = Fbm(u * 6f, v * 6f, 41);
                        float speck = Hash(x, y, 3) > 0.985f ? 0.6f : 0f;
                        a = Mathf.Clamp01(Mathf.SmoothStep(0.45f, 0.75f, n) * 0.35f + speck) * Mathf.Clamp01(1.2f - r);
                        c = new Color(0.32f, 0.29f, 0.24f);
                        break;
                    }
                }
                px[y * w + x] = new Color(c.r, c.g, c.b, a);
            }
            return px;
        }
    }
}
