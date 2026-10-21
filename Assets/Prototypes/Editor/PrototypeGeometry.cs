using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Prototypes.Editor
{
    /// <summary>Building blocks shared by the level builder: meshes, modular walls, colliders, lights.</summary>
    public static class PrototypeGeometry
    {
        public const float WallHeight = 3f;
        public const float WallThickness = 0.36f;
        public const float ClearWidth = 3.44f;      // matches the door stubs of the rooms
        public const float DuctWidth = 1.5f;
        public const float DuctHeight = 1.25f;      // crouched player 1.16 m fits, standing 1.40 m does not

        public const string Hallway = "Assets/Prefabs/Rooms/Props/Hallway/";
        public static readonly int Ground = LayerMask.NameToLayer("Ground");
        public static readonly int Walls = LayerMask.NameToLayer("Walls");
        public static readonly int Scenary = LayerMask.NameToLayer("Scenary");
        public static readonly int Props = LayerMask.NameToLayer("Props");
        public static readonly int Doors = LayerMask.NameToLayer("Doors");
        public const int DefaultLayer = 0; // carpets: flat on the floor, not props

        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string path)
        {
            if (PrefabCache.TryGetValue(path, out var p) && p != null) return p;
            p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) Debug.LogWarning("Protótipo: prefab não encontrado: " + path);
            PrefabCache[path] = p;
            return p;
        }

        public static GameObject Place(string path, Transform parent, Vector3 position, Quaternion rotation, Vector3? scale = null)
        {
            GameObject prefab = Prefab(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, rotation);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            return go;
        }

        public static GameObject Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public static void MarkStatic(GameObject go, bool includeNavigation = true)
        {
            var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponentInParent<Unity.Netcode.NetworkObject>(true) != null) continue;
                if (t.GetComponentInParent<Rigidbody>(true) != null) continue; // static batching would freeze what physics moves
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        /// <summary>
        /// Litter the players kick about as they run (cups, popcorn buckets): a light rigidbody on its
        /// own box, never static. Local physics only, not synced: each player sees their own kicks.
        /// </summary>
        public static void MakeLoose(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponentInChildren<Collider>(true) == null) FitBoxCollider(go, go.layer);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 0.08f;
            rb.linearDamping = 0.4f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        // ------------------------------------------------------------------ materials

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        /// <summary>The first material of a prefab's first renderer: reuses the project's own textures.</summary>
        public static Material MaterialOf(string prefabPath, int index = 0)
        {
            string key = prefabPath + "#" + index;
            if (MaterialCache.TryGetValue(key, out var m) && m != null) return m;
            var prefab = Prefab(prefabPath);
            var mr = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>(true) : null;
            m = mr != null && mr.sharedMaterials.Length > index ? mr.sharedMaterials[index] : null;
            MaterialCache[key] = m;
            return m;
        }

        public static Material Mat(string path)
        {
            if (MaterialCache.TryGetValue(path, out var m) && m != null) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) Debug.LogWarning("Protótipo: material não encontrado: " + path);
            MaterialCache[path] = m;
            return m;
        }

        public const string DefaultPaperPath = "Assets/Materials/Scenary/Hallway/Walls/V2/M_WallHallway_2.mat";

        /// <summary>
        /// The hallway wall pieces carry their wallpaper as M_WallHallway_2, except the column piece, which
        /// ships with M_WallHallway_1: swap either for another one. Matching only the first left every
        /// column in its own blue paper in the middle of a green or purple wing.
        /// </summary>
        public static void SwapPaper(GameObject go, Material paper)
        {
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = mr.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && (mats[i].name == "M_WallHallway_2" || mats[i].name == "M_WallHallway_1")) { mats[i] = paper; changed = true; }
                if (changed) mr.sharedMaterials = mats;
            }
        }

        // ------------------------------------------------------------------ props

        /// <summary>Wall props modelled with their back on x = 0 and the front towards local -x (posters, frames, boards).</summary>
        public static Quaternion FacingX(Vector3 inward) => Quaternion.LookRotation(Vector3.Cross(Vector3.up, inward), Vector3.up);

        /// <summary>Props whose front is local +z (signs, menus, mirror, sofas, lockers).</summary>
        public static Quaternion FacingZ(Vector3 inward) => Quaternion.LookRotation(inward, Vector3.up);

        /// <summary>Bounds of a prefab's meshes in its own local space (unscaled).</summary>
        public static Bounds LocalBounds(GameObject prefab)
        {
            Bounds b = new Bounds(); bool first = true;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>A box collider around the object's meshes (in its local space), capped in height.</summary>
        public static BoxCollider FitBoxCollider(GameObject go, int layer, float maxHeight = 99f)
        {
            if (go == null) return null;
            Bounds b = LocalBounds(go);
            if (b.size.sqrMagnitude < 1e-6f) return null;
            float h = Mathf.Min(b.size.y, maxHeight);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(b.center.x, b.min.y + h * 0.5f, b.center.z);
            col.size = new Vector3(b.size.x, h, b.size.z);
            go.layer = layer;
            return col;
        }

        /// <summary>Places a prefab so the centre of its footprint lands on floorPoint (for sets modelled away from their pivot).</summary>
        public static GameObject PlaceCentered(string path, Transform parent, Vector3 floorPoint, Quaternion rotation, Vector3? scale = null)
        {
            GameObject prefab = Prefab(path);
            if (prefab == null) return null;
            Bounds b = LocalBounds(prefab);
            Vector3 s = scale ?? Vector3.one;
            Vector3 offset = rotation * Vector3.Scale(new Vector3(b.center.x, b.min.y, b.center.z), s);
            return Place(path, parent, floorPoint - offset, rotation, scale);
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>A flat horizontal quad grid covering rects, UVs in metres so tiled textures keep their size.</summary>
        public static Mesh FlatMesh(IEnumerable<Rect> rects, float y, bool faceUp, float uvScale = 1f, string name = "Flat")
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var n = new List<Vector3>();
            var tri = new List<int>();
            foreach (Rect r in rects)
            {
                int i = v.Count;
                v.Add(new Vector3(r.xMin, y, r.yMin)); v.Add(new Vector3(r.xMax, y, r.yMin)); v.Add(new Vector3(r.xMax, y, r.yMax)); v.Add(new Vector3(r.xMin, y, r.yMax));
                foreach (var p in v.Skip(i)) { uv.Add(new Vector2(p.x, p.z) * uvScale); n.Add(faceUp ? Vector3.up : Vector3.down); }
                if (faceUp) tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
                else tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            var mesh = new Mesh { name = name };
            if (v.Count > 60000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            // UV0 tiles in metres; the lightmapper needs its own non-overlapping set.
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }

        /// <summary>An axis-aligned box mesh (outward faces), UVs in metres.</summary>
        public static Mesh BoxMesh(Vector3 size, float uvScale = 1f, bool inward = false, string name = "Box")
        {
            var mesh = new Mesh { name = name };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var n = new List<Vector3>();
            Vector3 h = size * 0.5f;
            void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
            {
                int i = v.Count;
                Vector3 c = Vector3.Scale(normal, h);
                Vector3 a = c - u * su * 0.5f - w * sw * 0.5f;
                v.Add(a); v.Add(a + u * su); v.Add(a + u * su + w * sw); v.Add(a + w * sw);
                uv.Add(Vector2.zero); uv.Add(new Vector2(su, 0) * uvScale); uv.Add(new Vector2(su, sw) * uvScale); uv.Add(new Vector2(0, sw) * uvScale);
                for (int k = 0; k < 4; k++) n.Add(inward ? -normal : normal);
                if (!inward) tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); else tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material, int layer, Vector3 position = default, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            return go;
        }

        public static BoxCollider BoxColliderAt(string name, Transform parent, Vector3 center, Vector3 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.layer = layer;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return bc;
        }

        // ------------------------------------------------------------------ walls

        public struct Opening
        {
            public float From, To;   // along the wall, metres from its start
            public float Top;        // height of the opening above the wall base (lintel starts here)
        }

        private static readonly string[] WallPieces4 = { "SM_Sophisticated_Wall_4m", "SM_Sophisticated_Wall_4m", "SM_Sophisticated_Wall_Column_4m" };

        /// <summary>
        /// A straight wall from start along dir, length long, with its decorated face towards the
        /// interior (Cross(dir, up) points outwards). Made of the project's modular hallway pieces,
        /// the last one scaled to fit; openings leave a gap with a lintel above when they are lower
        /// than the wall. One box collider per solid stretch.
        /// </summary>
        public static void Wall(Transform parent, Vector3 start, Vector3 dir, float length, float baseY, float height, IList<Opening> openings, int seed, bool collider = true, Material tint = null, Material paper = null, float rowHeight = WallHeight)
        {
            dir.y = 0; dir.Normalize();
            var solid = new List<(float a, float b, float y0, float h)>();
            float cursor = 0f;
            foreach (var o in (openings ?? new Opening[0]).OrderBy(o => o.From))
            {
                float from = Mathf.Clamp(o.From, 0f, length), to = Mathf.Clamp(o.To, 0f, length);
                if (from > cursor + 0.01f) solid.Add((cursor, from, 0f, height));
                if (o.Top < height - 0.05f && to > from) solid.Add((from, to, o.Top, height - o.Top));
                cursor = Mathf.Max(cursor, to);
            }
            if (cursor < length - 0.01f) solid.Add((cursor, length, 0f, height));

            // Local frame of the hallway pieces: run along local -z, decorated face on local -x (the interior).
            Quaternion rot = Quaternion.LookRotation(-dir, Vector3.up);
            Vector3 outward = Vector3.Cross(dir, Vector3.up).normalized * -1f; // local +x
            outward = rot * Vector3.right;

            int k = seed;
            foreach (var s in solid)
            {
                float len = s.b - s.a;
                if (len < 0.02f) continue;
                int rows = Mathf.CeilToInt(s.h / rowHeight - 0.01f);
                for (int row = 0; row < rows; row++)
                {
                    float rowY = baseY + s.y0 + row * rowHeight;
                    float rowH = Mathf.Min(rowHeight, s.h - row * rowHeight);
                    float yScale = rowH / WallHeight;
                    float pos = s.a;
                    while (pos < s.b - 0.01f)
                    {
                        float remain = s.b - pos;
                        string piece; float pieceLen;
                        if (remain >= 3.99f) { piece = WallPieces4[k++ % WallPieces4.Length]; pieceLen = 4f; }
                        else if (remain >= 1.99f) { piece = "SM_Sophisticated_Wall_2m"; pieceLen = 2f; }
                        else { piece = "SM_Sophisticated_Wall_2m"; pieceLen = remain; }
                        float scaleZ = piece.EndsWith("2m") && pieceLen < 2f ? pieceLen / 2f : 1f;
                        Vector3 at = start + dir * pos + Vector3.up * rowY;
                        var go = Place(Hallway + piece + ".prefab", parent, at, rot, new Vector3(1f, yScale, scaleZ));
                        if (go != null)
                        {
                            SetLayerRecursive(go, Walls);
                            if (tint != null) foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) { var mats = mr.sharedMaterials; if (mats.Length > 1) { mats[1] = tint; mr.sharedMaterials = mats; } }
                            // Always, even with the default paper: the column piece's own is a different one.
                            SwapPaper(go, paper != null ? paper : Mat(DefaultPaperPath));
                        }
                        pos += pieceLen;
                    }
                }

                if (!collider) continue;
                Vector3 mid = start + dir * ((s.a + s.b) * 0.5f) + outward * (WallThickness * 0.5f) + Vector3.up * (baseY + s.y0 + s.h * 0.5f);
                Vector3 size = new Vector3(Mathf.Abs(dir.x) > 0.5f ? len : WallThickness, s.h, Mathf.Abs(dir.x) > 0.5f ? WallThickness : len);
                BoxColliderAt("Col_Parede", parent, mid, size, Walls);
            }
        }

        // ------------------------------------------------------------------ grid union

        /// <summary>
        /// Union of axis-aligned rectangles on a grid made of their own edges (no rounding): which
        /// cells are inside, and the boundary between inside and outside as straight runs.
        /// </summary>
        public class RectUnion
        {
            public readonly float[] X, Z;
            public readonly bool[,] In;

            public RectUnion(IList<Rect> rects)
            {
                X = rects.SelectMany(r => new[] { r.xMin, r.xMax }).Select(v => Mathf.Round(v * 1000f) / 1000f).Distinct().OrderBy(v => v).ToArray();
                Z = rects.SelectMany(r => new[] { r.yMin, r.yMax }).Select(v => Mathf.Round(v * 1000f) / 1000f).Distinct().OrderBy(v => v).ToArray();
                In = new bool[Mathf.Max(0, X.Length - 1), Mathf.Max(0, Z.Length - 1)];
                for (int i = 0; i < X.Length - 1; i++)
                for (int j = 0; j < Z.Length - 1; j++)
                {
                    var c = new Vector2((X[i] + X[i + 1]) * 0.5f, (Z[j] + Z[j + 1]) * 0.5f);
                    In[i, j] = rects.Any(r => r.Contains(c));
                }
            }

            public IEnumerable<Rect> Cells()
            {
                // Greedy merge along x per row, then the rows: fewer quads and colliders.
                var used = new bool[In.GetLength(0), In.GetLength(1)];
                for (int j = 0; j < In.GetLength(1); j++)
                for (int i = 0; i < In.GetLength(0); i++)
                {
                    if (!In[i, j] || used[i, j]) continue;
                    int i1 = i;
                    while (i1 + 1 < In.GetLength(0) && In[i1 + 1, j] && !used[i1 + 1, j]) i1++;
                    int j1 = j;
                    while (j1 + 1 < In.GetLength(1))
                    {
                        bool ok = true;
                        for (int t = i; t <= i1; t++) if (!In[t, j1 + 1] || used[t, j1 + 1]) { ok = false; break; }
                        if (!ok) break;
                        j1++;
                    }
                    for (int a = i; a <= i1; a++) for (int b = j; b <= j1; b++) used[a, b] = true;
                    yield return Rect.MinMaxRect(X[i], Z[j], X[i1 + 1], Z[j1 + 1]);
                }
            }

            /// <summary>
            /// Boundary runs: start, direction (see Wall), length, inward normal. At an inner corner
            /// (three of the four cells around it inside) both walls would grow their thickness into
            /// the same outside corner and their faces overlap; trimReflex shortens the run along x
            /// there, so the run along z alone fills that corner.
            /// </summary>
            public IEnumerable<(Vector3 start, Vector3 dir, float length, Vector3 inward)> Boundary(float trimReflex = 0f)
            {
                foreach (var (start, dir, length, inward) in RawBoundary())
                {
                    if (trimReflex <= 0f || Mathf.Abs(dir.x) < 0.5f) { yield return (start, dir, length, inward); continue; }
                    Vector3 s = start, e = start + dir * length;
                    float trimStart = Reflex(s) ? trimReflex : 0f, trimEnd = Reflex(e) ? trimReflex : 0f;
                    float len = length - trimStart - trimEnd;
                    if (len < 0.02f) continue;
                    yield return (s + dir * trimStart, dir, len, inward);
                }
            }

            private bool Reflex(Vector3 p)
            {
                int i = System.Array.FindIndex(X, v => Mathf.Abs(v - p.x) < 0.002f);
                int j = System.Array.FindIndex(Z, v => Mathf.Abs(v - p.z) < 0.002f);
                if (i < 0 || j < 0) return false;
                int nx = In.GetLength(0), nz = In.GetLength(1);
                bool Get(int a, int b) => a >= 0 && b >= 0 && a < nx && b < nz && In[a, b];
                int n = (Get(i - 1, j - 1) ? 1 : 0) + (Get(i, j - 1) ? 1 : 0) + (Get(i - 1, j) ? 1 : 0) + (Get(i, j) ? 1 : 0);
                return n == 3;
            }

            private IEnumerable<(Vector3 start, Vector3 dir, float length, Vector3 inward)> RawBoundary()
            {
                int nx = In.GetLength(0), nz = In.GetLength(1);
                bool Get(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && In[i, j];

                // Horizontal edges (along x) between rows j-1 and j.
                for (int j = 0; j <= nz; j++)
                {
                    int i = 0;
                    while (i < nx)
                    {
                        bool below = Get(i, j - 1), above = Get(i, j);
                        if (below == above) { i++; continue; }
                        bool interiorAbove = above;
                        int i0 = i;
                        while (i < nx && Get(i, j - 1) != Get(i, j) && Get(i, j) == interiorAbove) i++;
                        float z = Z[j], x0 = X[i0], x1 = X[i];
                        Vector3 inward = interiorAbove ? Vector3.forward : Vector3.back;
                        // Wall runs along Cross(inward, up) so the decorated face looks inward.
                        Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                        Vector3 start = dir.x > 0 ? new Vector3(x0, 0, z) : new Vector3(x1, 0, z);
                        yield return (start, dir, x1 - x0, inward);
                    }
                }
                // Vertical edges (along z) between columns i-1 and i.
                for (int i = 0; i <= nx; i++)
                {
                    int j = 0;
                    while (j < nz)
                    {
                        bool left = Get(i - 1, j), right = Get(i, j);
                        if (left == right) { j++; continue; }
                        bool interiorRight = right;
                        int j0 = j;
                        while (j < nz && Get(i - 1, j) != Get(i, j) && Get(i, j) == interiorRight) j++;
                        float x = X[i], z0 = Z[j0], z1 = Z[j];
                        Vector3 inward = interiorRight ? Vector3.right : Vector3.left;
                        Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                        Vector3 start = dir.z > 0 ? new Vector3(x, 0, z0) : new Vector3(x, 0, z1);
                        yield return (start, dir, z1 - z0, inward);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ lights

        public static GameObject CeilingLight(Transform parent, Vector3 floorPoint, float ceiling, Color color, float intensity, float range, bool on, List<GameObject> sabotage)
        {
            var root = Group("Luz_Teto", parent);
            root.transform.position = floorPoint + Vector3.up * ceiling;
            var lamp = Place(Hallway + "SM_Hallway_Light_Roof.prefab", root.transform, root.transform.position, Quaternion.identity);
            if (lamp != null) SetLayerRecursive(lamp, Scenary);
            var light = Place("Assets/Prefabs/Managers/SabotageObjects/SabotageLight.prefab", root.transform, root.transform.position + Vector3.down * 0.85f, Quaternion.identity);
            if (light != null)
            {
                var l = light.GetComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.range = range;
                l.shadows = LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Mixed;
                l.enabled = on;
                sabotage?.Add(light);
            }
            return root;
        }
    }
}
