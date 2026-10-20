using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// The clean-up a person did by hand on Game1 after it was generated, as rules: dressing is
    /// placed on a rhythm along the walls and does not know what else ends up there.
    /// - Wall-hung pieces (posters, frames, boards, papers) must not cut into a column's pilaster,
    ///   a wall lamp, a door or each other. A paper pinned on top of a board is fine.
    /// - Free-standing props must not cut into a pilaster or a wall lamp.
    /// Whatever clashes slides along its wall to the nearest clear spot (up to 2.5 m, still in front
    /// of a wall); if there is none, it goes. The Bilheteria's furniture is laid out by hand, so it
    /// only ever slides.
    /// </summary>
    public static class PrototypeTidy
    {
        private static readonly string[] WallHung = { "SM_Poster", "SM_Frame_", "SM_Notice_Board", "SM_Paper_", "SM_Post_Its", "SM_Mirror" };
        private static readonly string[] Fixed = { "SM_Exit_Light", "SM_Carpet_", "SM_Sophisticated_Frame" };
        private static readonly string[] AudienceProps = { "Sword", "SM_Crown", "Happy_Mask_Low", "Sad_Mask_Low" };

        private class Item
        {
            public Transform T;
            public Bounds B;
            public bool Hung, Keep;
            public int Axis;          // world axis it may slide along (0 = x, 2 = z)
            public Vector3 Inward;    // from its wall into the corridor (hung pieces)
        }

        public static string Run(PrototypeBuilder.Context ctx)
        {
            Physics.SyncTransforms();
            var obstacles = new List<Bounds>();
            obstacles.AddRange(Pilasters(ctx.Root));
            obstacles.AddRange(RenderBounds(ctx.Root.Find("LuzesCorredor")));
            obstacles.AddRange(RenderBounds(ctx.Root.Find("PortasCorredor")));

            var items = new List<Item>();
            foreach (string group in new[] { "Decoracao", "Bilheteria_Mobilia" })
            {
                Transform g = ctx.Root.Find(group);
                if (g == null) continue;
                foreach (Transform t in g)
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    Bounds b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    string name = SourceName(t);
                    bool flatOnFloor = b.size.y < 0.03f;
                    bool hung = !flatOnFloor && WallHung.Any(name.StartsWith);
                    var item = new Item { T = t, B = b, Hung = hung, Keep = group == "Bilheteria_Mobilia" };
                    item.Axis = b.size.x < b.size.z ? 2 : 0;
                    if (Fixed.Any(name.StartsWith) || AudienceProps.Any(name.StartsWith) || flatOnFloor) { obstacles.Add(b); continue; }
                    items.Add(item);
                }
            }

            // Props carrying something worth filming are fixed: the crate under each audience object.
            foreach (Item it in items.ToList())
            {
                if (it.Hung) continue;
                if (obstacles.Any(o => o.size.y < 0.6f && Mathf.Abs(o.min.y - it.B.max.y) < 0.08f && Overlap2D(o, it.B)))
                {
                    obstacles.Add(it.B);
                    items.Remove(it);
                }
            }

            int moved = 0, removed = 0;
            var accepted = new List<Item>();

            // Wall-hung pieces, biggest first: the smaller ones make room.
            foreach (Item it in items.Where(i => i.Hung).OrderByDescending(i => Area(i.B, i.Axis)))
            {
                it.Inward = WallInward(it);
                bool Clear(Bounds b) => !obstacles.Any(o => Hits(o, b)) && !accepted.Any(a => HungClash(a, b, it.Axis, it.Inward));
                switch (Settle(it, Clear, b => it.Inward == Vector3.zero || WallBehind(b, it.Inward)))
                {
                    case 1: moved++; accepted.Add(it); break;
                    case 0: accepted.Add(it); break;
                    default: if (it.Keep) accepted.Add(it); else { Object.DestroyImmediate(it.T.gameObject); removed++; } break;
                }
            }

            // Free-standing props against pilasters and lamps.
            foreach (Item it in items.Where(i => !i.Hung))
            {
                it.Axis = CorridorAxis(ctx.Layout, it.B.center, it.Axis);
                bool Clear(Bounds b) => !obstacles.Any(o => Hits(o, b));
                switch (Settle(it, Clear, _ => true))
                {
                    case 1: moved++; break;
                    case -1: if (!it.Keep) { Object.DestroyImmediate(it.T.gameObject); removed++; } break;
                }
            }
            return $"  arrumação: {moved} peças deslizadas, {removed} removidas (pilastras, luminárias, portas, sobreposição)";
        }

        /// <summary>0 = already clear, 1 = slid to a clear spot, -1 = no clear spot within reach.</summary>
        private static int Settle(Item it, System.Func<Bounds, bool> clear, System.Func<Bounds, bool> valid)
        {
            if (clear(it.B)) return 0;
            Vector3 axis = it.Axis == 0 ? Vector3.right : Vector3.forward;
            for (float d = 0.25f; d <= 2.5f; d += 0.25f)
            foreach (float s in new[] { d, -d })
            {
                Bounds b = it.B;
                b.center += axis * s;
                if (!clear(b) || !valid(b)) continue;
                it.T.position += axis * s;
                it.B = b;
                return 1;
            }
            return -1;
        }

        private static bool Hits(Bounds o, Bounds b)
        {
            const float t = 0.01f;
            return o.min.x < b.max.x - t && b.min.x < o.max.x - t && o.min.y < b.max.y - t && b.min.y < o.max.y - t && o.min.z < b.max.z - t && b.min.z < o.max.z - t;
        }

        private static bool Overlap2D(Bounds a, Bounds b) => a.min.x < b.max.x && b.min.x < a.max.x && a.min.z < b.max.z && b.min.z < a.max.z;

        private static float Area(Bounds b, int axis) => (axis == 0 ? b.size.x : b.size.z) * b.size.y;

        /// <summary>Two hung pieces on the same wall clash when they overlap in the wall's plane, unless the smaller one is pinned on top of the bigger.</summary>
        private static bool HungClash(Item big, Bounds small, int axis, Vector3 inward)
        {
            int lat = axis == 0 ? 2 : 0;
            if (Mathf.Abs(big.B.center[lat] - small.center[lat]) > 0.3f) return false; // other wall
            float along = Mathf.Min(big.B.max[axis], small.max[axis]) - Mathf.Max(big.B.min[axis], small.min[axis]);
            float up = Mathf.Min(big.B.max.y, small.max.y) - Mathf.Max(big.B.min.y, small.min.y);
            if (along <= 0.02f || up <= 0.02f) return false;
            bool inside = small.min[axis] >= big.B.min[axis] - 0.02f && small.max[axis] <= big.B.max[axis] + 0.02f && small.min.y >= big.B.min.y - 0.02f && small.max.y <= big.B.max.y + 0.02f;
            bool inFront = inward != Vector3.zero && Vector3.Dot(small.center - big.B.center, inward) > 0f;
            if (inside && inFront) return false;
            // Even a corner of a paper over a post-it read as a mess (straightened by hand on Game2/Game5).
            float smallArea = Mathf.Max(0.0001f, (small.size[axis]) * small.size.y);
            return along * up > smallArea * 0.02f;
        }

        /// <summary>The direction from the wall a hung piece is on into the room, or zero when no wall collider is found.</summary>
        private static Vector3 WallInward(Item it)
        {
            Vector3 n = it.Axis == 0 ? Vector3.forward : Vector3.right;
            int mask = 1 << Walls;
            Vector3 c = it.B.center;
            bool front = Physics.Raycast(c, n, out RaycastHit hf, 0.6f, mask, QueryTriggerInteraction.Ignore);
            bool back = Physics.Raycast(c, -n, out RaycastHit hb, 0.6f, mask, QueryTriggerInteraction.Ignore);
            if (front && (!back || hf.distance < hb.distance)) return -n;
            if (back) return n;
            return Vector3.zero;
        }

        private static bool WallBehind(Bounds b, Vector3 inward) =>
            Physics.Raycast(b.center + inward * 0.2f, -inward, 0.8f, 1 << Walls, QueryTriggerInteraction.Ignore);

        /// <summary>Along the corridor the prop stands in; its own long side when it is not in one.</summary>
        private static int CorridorAxis(PrototypeLayout l, Vector3 p, int fallback)
        {
            foreach (WPiece piece in l.Pieces)
            {
                if (piece.Color == BarColor.Yellow) continue;
                if (Mathf.Abs(piece.FloorA - p.y) > 2.5f && Mathf.Abs(piece.FloorB - p.y) > 2.5f) continue;
                if (PrototypeLayout.PieceRect(piece, 0f).Contains(new Vector2(p.x, p.z))) return piece.Horizontal ? 0 : 2;
            }
            return fallback;
        }

        /// <summary>The pilaster of every column piece: the part that stands proud of the wall, mid-length.</summary>
        private static IEnumerable<Bounds> Pilasters(Transform root)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.sharedMesh.name != "Parede_Detalhada_Coluna_4m.001") continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Bounds b = new Bounds(m.MultiplyPoint3x4(new Vector3(-0.125f, 0f, -2.45f)), Vector3.zero);
                b.Encapsulate(m.MultiplyPoint3x4(new Vector3(0.076f, 3f, -1.55f)));
                yield return b;
            }
        }

        private static IEnumerable<Bounds> RenderBounds(Transform group)
        {
            if (group == null) yield break;
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true)) yield return r.bounds;
        }

        private static string SourceName(Transform t)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            return src != null ? src.name : t.name;
        }
    }
}
