using System.IO;
using System.Linq;
using LevelDesign.Toolkit;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Gym.Building
{
    /// <summary>Blockout primitives for the GYM generator: walls, boxes, ramps, labels, pads.</summary>
    public class GymBuildKit
    {
        public const string MaterialFolder = "Assets/LevelDesign/Gym/Materials";
        public const float WallHeight = 3f;
        public const float WallThickness = 0.3f;

        public readonly int LayerDefault = 0;
        public readonly int LayerIgnoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        public readonly int LayerGround = LayerMask.NameToLayer("Ground");
        public readonly int LayerWalls = LayerMask.NameToLayer("Walls");
        public readonly int LayerScenary = LayerMask.NameToLayer("Scenary");
        public readonly int LayerProps = LayerMask.NameToLayer("Props");

        public Material Floor { get; private set; }
        public Material WallMat { get; private set; }
        public Material Height { get; private set; }
        public Material Crouch { get; private set; }
        public Material Cover { get; private set; }
        public Material Warning { get; private set; }
        public Material Table { get; private set; }
        public Material PadRed { get; private set; }
        public Material PadBlue { get; private set; }
        public Material PadGreen { get; private set; }
        public Material PadYellow { get; private set; }
        public Material Stripe { get; private set; }
        public Material StripeSpecial { get; private set; }

        private readonly TMP_FontAsset _font;
        private readonly LevelToolkitCatalog _catalog;

        public GymBuildKit(LevelToolkitCatalog catalog)
        {
            _catalog = catalog;
            _font = AssetDatabase.FindAssets("Acme-Regular SDF t:TMP_FontAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(f => f != null) ?? TMP_Settings.defaultFontAsset;

            LevelToolkitScanner.EnsureFolder(MaterialFolder);

            Floor = Grid("Gym_Floor", new Color(0.40f, 0.41f, 0.45f), new Color(0.30f, 0.31f, 0.35f), new Color(0.16f, 0.16f, 0.2f));
            WallMat = Grid("Gym_Wall", new Color(0.70f, 0.71f, 0.74f), new Color(0.58f, 0.59f, 0.63f), new Color(0.38f, 0.38f, 0.42f));
            Height = Grid("Gym_Height", new Color(0.86f, 0.55f, 0.25f), new Color(0.72f, 0.43f, 0.18f), new Color(0.45f, 0.25f, 0.1f));
            Crouch = Grid("Gym_Crouch", new Color(0.32f, 0.52f, 0.85f), new Color(0.24f, 0.41f, 0.7f), new Color(0.12f, 0.22f, 0.45f));
            Cover = Grid("Gym_Cover", new Color(0.35f, 0.68f, 0.45f), new Color(0.27f, 0.55f, 0.36f), new Color(0.14f, 0.33f, 0.2f));
            Warning = Grid("Gym_Warning", new Color(0.72f, 0.36f, 0.78f), new Color(0.58f, 0.27f, 0.64f), new Color(0.35f, 0.14f, 0.4f));
            Table = Grid("Gym_Table", new Color(0.5f, 0.42f, 0.34f), new Color(0.42f, 0.35f, 0.28f), new Color(0.3f, 0.24f, 0.18f));

            PadRed = Unlit("Gym_Pad_Red", new Color(0.9f, 0.22f, 0.2f));
            PadBlue = Unlit("Gym_Pad_Blue", new Color(0.25f, 0.6f, 1f));
            PadGreen = Unlit("Gym_Pad_Green", new Color(0.3f, 0.85f, 0.4f));
            PadYellow = Unlit("Gym_Pad_Yellow", new Color(1f, 0.8f, 0.2f));
            Stripe = Unlit("Gym_Stripe", new Color(1f, 0.85f, 0.25f));
            StripeSpecial = Unlit("Gym_Stripe_Special", new Color(0.3f, 0.95f, 0.95f));
        }

        // ---------------------------------------------------------------- materials

        private static Material Grid(string name, Color baseColor, Color line, Color major)
        {
            var shader = Shader.Find("TCC/LevelDesign/GymGrid") ?? Shader.Find("Universal Render Pipeline/Lit");
            var material = LoadOrCreate(name, shader);
            material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_LineColor")) material.SetColor("_LineColor", line);
            if (material.HasProperty("_MajorColor")) material.SetColor("_MajorColor", major);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Unlit(string name, Color color)
        {
            var material = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (material.shader != shader) material.shader = shader;
                return material;
            }

            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------------------------------------------------------------- geometry

        public GameObject Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Axis-aligned box given by its bottom-centre, so heights read the way you think of them.</summary>
        public GameObject Box(Transform parent, string name, Vector3 bottomCenter, Vector3 size, Material material, int layer, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = bottomCenter + Vector3.up * (size.y * 0.5f);
            go.transform.localScale = size;
            go.layer = layer;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>A wall between two points on the floor (XZ), closed at the corners.</summary>
        public GameObject Wall(Transform parent, Vector3 a, Vector3 b, float height = WallHeight, float baseY = 0f, Material material = null, string name = "Parede")
        {
            Vector3 flat = b - a;
            flat.y = 0f;
            float length = flat.magnitude + WallThickness;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f + Vector3.up * (baseY + height * 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            go.transform.localScale = new Vector3(WallThickness, height, length);
            go.layer = LayerWalls;
            go.GetComponent<MeshRenderer>().sharedMaterial = material ?? WallMat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>Wall along X at fixed z, from x0 to x1, leaving the given gaps (centre ± half width) open.</summary>
        public void WallAlongX(Transform parent, float z, float x0, float x1, float gapWidth, params float[] gapCenters)
        {
            float cursor = x0;
            foreach (float center in gapCenters.OrderBy(c => c))
            {
                float gapStart = center - gapWidth * 0.5f;
                if (gapStart > cursor + 0.01f) Wall(parent, new Vector3(cursor, 0f, z), new Vector3(gapStart, 0f, z));
                cursor = center + gapWidth * 0.5f;
            }

            if (x1 > cursor + 0.01f) Wall(parent, new Vector3(cursor, 0f, z), new Vector3(x1, 0f, z));
        }

        /// <summary>
        /// A slab whose top surface climbs from <paramref name="start"/> (on the floor) to
        /// <paramref name="height"/> at the given angle, plus a 2 m landing at the top.
        /// </summary>
        public float Ramp(Transform parent, string name, Vector3 start, Vector3 direction, float width, float height, float angle)
        {
            const float thickness = 0.25f;
            direction.y = 0f;
            direction.Normalize();

            float run = height / Mathf.Tan(angle * Mathf.Deg2Rad);
            float slopeLength = Mathf.Sqrt(run * run + height * height);
            Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(-angle, 0f, 0f);
            Vector3 surfaceMid = start + direction * (run * 0.5f) + Vector3.up * (height * 0.5f);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.SetParent(parent, false);
            ramp.transform.localRotation = rotation;
            ramp.transform.localPosition = surfaceMid - rotation * Vector3.up * (thickness * 0.5f);
            ramp.transform.localScale = new Vector3(width, thickness, slopeLength + 0.05f);
            ramp.layer = LayerGround;
            ramp.GetComponent<MeshRenderer>().sharedMaterial = Height;

            Vector3 landing = start + direction * (run + 1f);
            Box(parent, name + " (topo)", landing, new Vector3(Mathf.Abs(direction.x) > 0.5f ? 2f : width, height, Mathf.Abs(direction.z) > 0.5f ? 2f : width), Height, LayerGround);
            return run;
        }

        // ---------------------------------------------------------------- labels

        public static Quaternion FacingViewer(Vector3 towardsViewer) => Quaternion.LookRotation(-towardsViewer.normalized, Vector3.up);

        public static Quaternion OnFloor(Vector3 readingDirection) =>
            Quaternion.LookRotation(Vector3.down, readingDirection.normalized);

        public TextMeshPro Label(Transform parent, string text, Vector3 position, Quaternion rotation, float fontSize, float width = 6f, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.layer = LayerDefault;

            var tmp = go.AddComponent<TextMeshPro>();
            if (_font != null) tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.color = color ?? Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(width, fontSize * 0.3f * (1 + text.Count(c => c == '\n')));
            tmp.overflowMode = TextOverflowModes.Overflow;

            // Boards hang from their top edge; everything else is centred on its position.
            if (alignment is TextAlignmentOptions.TopLeft or TextAlignmentOptions.Top) tmp.rectTransform.pivot = new Vector2(0.5f, 1f);
            return tmp;
        }

        public void FloorStripe(Transform parent, Vector3 center, Vector3 along, float length, float width, Material material)
        {
            var go = Box(parent, "Faixa", center, Vector3.one, material, LayerDefault, collider: false);
            go.transform.localRotation = Quaternion.LookRotation(along.normalized);
            go.transform.localScale = new Vector3(width, 0.01f, length);
            go.transform.localPosition = center + Vector3.up * 0.006f;
        }

        // ---------------------------------------------------------------- pads

        public GymPad Pad(Transform parent, string label, Vector3 center, Vector2 size, GymPadAction action, Transform target, Material material, Vector3 readingDirection)
        {
            var go = new GameObject($"Pad · {label}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.layer = LayerIgnoreRaycast;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, 2.5f, size.y);
            trigger.center = new Vector3(0f, 1.25f, 0f);

            var plate = Box(go.transform, "Placa", Vector3.zero, new Vector3(size.x, 0.03f, size.y), material, LayerIgnoreRaycast, collider: false);
            plate.transform.localPosition = new Vector3(0f, 0.016f, 0f);

            Label(go.transform, label, new Vector3(0f, 0.04f, 0f), OnFloor(readingDirection), 2.2f, Mathf.Max(size.x, size.y) * 0.95f, Color.white);

            var pad = go.AddComponent<GymPad>();
            pad.Configure(action, target, label);
            return pad;
        }

        public Transform Marker(Transform parent, string name, Vector3 position, Vector3 forward)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(forward.normalized);
            return go.transform;
        }

        // ---------------------------------------------------------------- prefabs

        public GameObject PlacePrefab(Transform parent, string assetPath, Vector3 bottomCenter, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"GYM: prefab não encontrado: {assetPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localPosition = Vector3.zero;

            // Pivots differ from prefab to prefab: line the renderer bounds up instead.
            var bounds = LevelToolkitScanner.RendererBounds(instance);
            if (bounds.size != Vector3.zero)
            {
                Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                instance.transform.position += parent.TransformPoint(bottomCenter) - bottom;
            }
            else
            {
                instance.transform.localPosition = bottomCenter;
            }

            return instance;
        }

        public string CodeOf(string assetPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return _catalog != null ? _catalog.FindByGuid(guid)?.code ?? string.Empty : string.Empty;
        }

        public static string NameOf(string assetPath) => LevelToolkitScanner.Prettify(Path.GetFileNameWithoutExtension(assetPath));
    }
}
