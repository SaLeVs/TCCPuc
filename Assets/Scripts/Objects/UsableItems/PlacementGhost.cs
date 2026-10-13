using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Objects.UsableItems
{
    /// <summary>
    /// A see-through copy of an item, shown where it would land before it is placed, with a dashed
    /// outline of its base on the floor.
    /// </summary>
    /// <remarks>
    /// Built from the meshes alone: no colliders, so it never gets between the player's aim and the
    /// slot it previews, and no network or gameplay components, so it is nothing but a picture.
    /// Purely local - only the player holding the item ever sees it.
    /// </remarks>
    public class PlacementGhost : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FootprintId = Shader.PropertyToID("_Footprint");
        private static readonly int FootprintSizeId = Shader.PropertyToID("_FootprintSize");

        // Room between the item's base and the dashed line, in meters.
        private const float FOOTPRINT_MARGIN = 0.05f;

        // Above the surface the item rests on, so the outline never z-fights with it.
        private const float FOOTPRINT_LIFT = 0.003f;

        private Renderer[] _parts;
        private Renderer _footprint;
        private Mesh _footprintMesh;
        private MaterialPropertyBlock _partBlock;
        private MaterialPropertyBlock _footprintBlock;
        private Color _color;
        private bool _hasColor;

        /// <summary>
        /// Copies every mesh of <paramref name="source"/>, laid out exactly as it is in the prefab, so
        /// that placing the ghost where the item spawns lines the two up.
        /// </summary>
        public static PlacementGhost Build(GameObject source, Material material)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");

            var root = new GameObject($"{source.name} (Ghost)");
            root.SetActive(false);
            root.layer = layer;

            // Instantiate keeps the prefab root's scale, so the ghost has to as well.
            root.transform.localScale = source.transform.localScale;

            var ghost = root.AddComponent<PlacementGhost>();
            var parts = new List<Renderer>();
            Matrix4x4 toRoot = source.transform.worldToLocalMatrix;
            Bounds footprint = default;
            bool hasBounds = false;

            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;

                if (mesh == null) continue;
                if (!filter.TryGetComponent(out MeshRenderer sourceRenderer) || !sourceRenderer.enabled) continue;

                Matrix4x4 local = toRoot * filter.transform.localToWorldMatrix;

                var part = new GameObject(filter.name) { layer = layer };
                part.transform.SetParent(root.transform, false);
                part.transform.SetLocalPositionAndRotation(local.GetPosition(), local.rotation);
                part.transform.localScale = local.lossyScale;

                part.AddComponent<MeshFilter>().sharedMesh = mesh;

                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;

                parts.Add(AddRenderer(part, materials));

                Encapsulate(ref footprint, ref hasBounds, local, mesh.bounds);
            }

            ghost._parts = parts.ToArray();
            ghost._partBlock = new MaterialPropertyBlock();
            ghost._footprintBlock = new MaterialPropertyBlock();

            if (hasBounds)
            {
                ghost._footprint = BuildFootprint(root, footprint, material, layer, out ghost._footprintMesh, out Vector2 size);

                ghost._footprintBlock.SetFloat(FootprintId, 1f);
                ghost._footprintBlock.SetVector(FootprintSizeId, new Vector4(size.x, size.y, 0f, 0f));
            }

            return ghost;
        }

        private static Renderer AddRenderer(GameObject target, Material[] materials)
        {
            var meshRenderer = target.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return meshRenderer;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool hasBounds, Matrix4x4 local, Bounds meshBounds)
        {
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z));

                if (hasBounds) bounds.Encapsulate(point);
                else bounds = new Bounds(point, Vector3.zero);

                hasBounds = true;
            }
        }

        /// <summary>
        /// A flat quad under the item's base. Its UVs are in meters, so the shader draws dashes of the
        /// same size around a small item and a large one.
        /// </summary>
        private static Renderer BuildFootprint(GameObject root, Bounds bounds, Material material, int layer,
            out Mesh mesh, out Vector2 size)
        {
            float minX = bounds.min.x - FOOTPRINT_MARGIN;
            float maxX = bounds.max.x + FOOTPRINT_MARGIN;
            float minZ = bounds.min.z - FOOTPRINT_MARGIN;
            float maxZ = bounds.max.z + FOOTPRINT_MARGIN;
            float y = bounds.min.y + FOOTPRINT_LIFT;

            Vector3 scale = root.transform.localScale;
            float width = (maxX - minX) * Mathf.Abs(scale.x);
            float depth = (maxZ - minZ) * Mathf.Abs(scale.z);

            mesh = new Mesh
            {
                name = "Footprint",
                vertices = new[]
                {
                    new Vector3(minX, y, minZ), new Vector3(maxX, y, minZ),
                    new Vector3(maxX, y, maxZ), new Vector3(minX, y, maxZ)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(width, 0f),
                    new Vector2(width, depth), new Vector2(0f, depth)
                },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };

            mesh.RecalculateBounds();

            var footprint = new GameObject("Footprint") { layer = layer };
            footprint.transform.SetParent(root.transform, false);
            footprint.AddComponent<MeshFilter>().sharedMesh = mesh;

            Renderer footprintRenderer = AddRenderer(footprint, new[] { material });

            // Drawn before the hologram, whose depth pass would otherwise hide the dashes behind it.
            footprintRenderer.sortingOrder = -1;

            size = new Vector2(width, depth);
            return footprintRenderer;
        }

        /// <summary>Stands the ghost on <paramref name="point"/>, tinted <paramref name="color"/>.</summary>
        public void Show(Transform point, Color color)
        {
            transform.SetPositionAndRotation(point.position, point.rotation);

            if (!_hasColor || color != _color)
            {
                _color = color;
                _hasColor = true;

                _partBlock.SetColor(ColorId, color);
                foreach (Renderer part in _parts) part.SetPropertyBlock(_partBlock);

                if (_footprint != null)
                {
                    _footprintBlock.SetColor(ColorId, color);
                    _footprint.SetPropertyBlock(_footprintBlock);
                }
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // The footprint's mesh is made at runtime, so it goes when the ghost does.
        private void OnDestroy()
        {
            if (_footprintMesh == null) return;

            if (Application.isPlaying) Destroy(_footprintMesh);
            else DestroyImmediate(_footprintMesh);
        }
    }
}
