using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// Dresses the corridors following the level design notes: every prop answers "why is it here
    /// for gameplay" and "why is it here for the world". Exit signs point up the stairs towards the
    /// Bilheteria and the Escape (direction), benches and boxes break long runs (diversity and chase
    /// obstacles), theatre posters and frames tell where you are without text (show, don't tell), and
    /// the objects worth filming sit where it is risky to stop (desire: risk for reward).
    /// </summary>
    public static class PrototypeDressing
    {
        private const string Props = "Assets/Prefabs/Rooms/Props/";
        private static readonly string[] WallArt = { "SM_Poster", "SM_Poster_V2", "SM_Frame_V1", "SM_Frame_V2", "SM_Notice_Board" };
        private static readonly string[] Carpets = { "SM_Carpet_01", "SM_Carpet_02", "SM_Carpet_03", "SM_Carpet_05", "SM_Carpet_06" };

        public static void Dress(PrototypeBuilder.Context ctx)
        {
            var root = Group("Decoracao", ctx.Root).transform;
            var rnd = new System.Random(ctx.Level * 977);
            PrototypeLayout l = ctx.Layout;
            PrototypeCorridors c = ctx.Corridors;

            // Places along a corridor where both walls are solid: away from crossings, doors, stairs and ducts.
            var blocked = new List<Vector3>();
            blocked.AddRange(c.Stairs.SelectMany(s => new[] { s.Top, s.Bottom }));
            blocked.AddRange(c.Ducts.SelectMany(d => new[] { d.A, d.B }));
            blocked.AddRange(c.DoorSpots.Select(d => d.pos));
            blocked.AddRange(l.Rooms.SelectMany(r => r.DoorPoints.Values));
            foreach (WPiece p in l.Pieces)
                foreach (WPiece o in l.Pieces.Where(o => o != p && o.Horizontal != p.Horizontal && p.Bar.Touching.Contains(o.Bar)))
                    blocked.Add(p.Horizontal ? new Vector3(o.Line, p.FloorA, p.Line) : new Vector3(p.Line, p.FloorA, o.Line));

            bool Free(Vector3 pos, float clearance) => blocked.All(b => Mathf.Abs(b.y - pos.y) > 1.5f || Vector2.Distance(new Vector2(b.x, b.z), new Vector2(pos.x, pos.z)) > clearance);

            int art = 0, benches = 0, carpets = 0, audience = 0, signs = 0, boxes = 0;
            foreach (WPiece p in l.Pieces.Where(p => p.Color != BarColor.Yellow && p.Length > 8f))
            {
                Vector3 along = p.Horizontal ? Vector3.right : Vector3.forward;
                Vector3 across = p.Horizontal ? Vector3.forward : Vector3.right;
                bool upper = p.FloorA > 1f;
                for (float t = p.A + 3.5f; t < p.B - 3.5f; t += 5.5f + (float)rnd.NextDouble() * 3f)
                {
                    Vector3 floor = p.Horizontal ? new Vector3(t, p.FloorA, p.Line) : new Vector3(p.Line, p.FloorA, t);
                    if (!Free(floor, 3.2f)) continue;
                    float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                    Vector3 inward = -across * side;                           // from that wall towards the corridor
                    Vector3 wallPoint = floor + across * side * (ClearWidth * 0.5f);
                    Quaternion facing = Quaternion.LookRotation(Vector3.Cross(Vector3.up, inward), Vector3.up);
                    double roll = rnd.NextDouble();

                    if (roll < 0.42)
                    {
                        string piece = WallArt[rnd.Next(WallArt.Length)];
                        float h = piece == "SM_Notice_Board" ? 0.9f : piece.StartsWith("SM_Poster") ? 1.1f : 0.95f;
                        var go = Place(Props + "Hallway/" + piece + ".prefab", root, wallPoint + Vector3.up * h, piece == "SM_Notice_Board" ? Quaternion.LookRotation(inward, Vector3.up) : facing);
                        if (go != null) { SetLayerRecursive(go, Scenary); art++; }
                    }
                    else if (roll < 0.62)
                    {
                        string bench = rnd.NextDouble() < 0.5 ? "SM_Small_Bench" : "SM_Big_Bench";
                        var go = Place(Props + "Hallway/" + bench + ".prefab", root, wallPoint + inward * 0.42f, Quaternion.LookRotation(along, Vector3.up));
                        if (go != null) { SetLayerRecursive(go, PrototypeGeometry.Props); benches++; }
                    }
                    else if (roll < 0.74 && !upper)
                    {
                        // A stack of boxes against the wall: cover in a chase, clutter of a working theatre.
                        var go = Place(FindProp("SM_Cardboard_Box"), root, wallPoint + inward * 0.5f, Quaternion.Euler(0f, rnd.Next(0, 360), 0f));
                        if (go != null) { SetLayerRecursive(go, Scenary); boxes++; }
                    }

                    if (upper && rnd.NextDouble() < 0.55)
                    {
                        var go = Place(Props + "Hallway/" + Carpets[rnd.Next(Carpets.Length)] + ".prefab", root, floor + Vector3.up * 0.005f, Quaternion.LookRotation(along, Vector3.up), new Vector3(2.2f, 1f, 2.2f));
                        if (go != null) { SetLayerRecursive(go, Scenary); carpets++; }
                    }
                }
            }

            // Exit signs over every stair foot and at the auditorium side doors: the way up is the way out.
            foreach (var s in c.Stairs)
            {
                Vector3 over = s.Bottom + Vector3.up * 2.62f - s.Dir * 0.02f;
                var go = Place(Props + "Hallway/SM_Exit_Light.prefab", root, over, Quaternion.LookRotation(-s.Dir, Vector3.up));
                if (go != null) { SetLayerRecursive(go, Scenary); signs++; }
            }
            foreach (float x in new[] { -16.4f, 16.4f })
            {
                var go = Place(Props + "Hallway/SM_Exit_Light.prefab", root, new Vector3(x, 2.62f, -1.85f), Quaternion.LookRotation(new Vector3(Mathf.Sign(x), 0f, 0f), Vector3.up));
                if (go != null) { SetLayerRecursive(go, Scenary); signs++; }
            }

            // Worth filming, risky to stop for: at duct mouths (the monster cannot follow, but you are cornered
            // until you crawl) and next to dead-end bathrooms.
            string[] valuable = { "Sword", "SM_Crown", "Happy_Mask_Low", "Sad_Mask_Low" };
            var spots = new List<Vector3>();
            spots.AddRange(c.Ducts.Select(d => d.A.y < 1f ? d.A : d.B));
            spots.AddRange(l.Rooms.Where(r => r.Room.Kind == RoomKind.Bathroom).SelectMany(r => r.DoorPoints.Values));
            foreach (Vector3 spot in spots.Take(6))
            {
                // On a crate against the corridor wall beside the mouth/door: never floating, never in the way.
                WPiece host = l.Pieces.Where(p => p.Color != BarColor.Yellow && Mathf.Abs(p.FloorA - spot.y) < 1f)
                    .OrderBy(p => p.Horizontal ? Mathf.Abs(p.Line - spot.z) + Mathf.Max(0f, p.A - spot.x) + Mathf.Max(0f, spot.x - p.B)
                                               : Mathf.Abs(p.Line - spot.x) + Mathf.Max(0f, p.A - spot.z) + Mathf.Max(0f, spot.z - p.B)).FirstOrDefault();
                if (host == null) continue;
                Vector3 along = host.Horizontal ? Vector3.right : Vector3.forward;
                Vector3 across = host.Horizontal ? Vector3.forward : Vector3.right;
                Vector3 onLine = host.Horizontal ? new Vector3(spot.x, host.FloorA, host.Line) : new Vector3(host.Line, host.FloorA, spot.z);
                float sideSign = Vector3.Dot(spot - onLine, across) >= 0f ? -1f : 1f; // the wall opposite the mouth
                Vector3 at = onLine + along * 1.6f + across * sideSign * (ClearWidth * 0.5f - 0.45f);
                var crate = Place(FindProp("SM_Cardboard_Box"), root, at, Quaternion.Euler(0f, audience * 37f, 0f));
                if (crate != null) SetLayerRecursive(crate, Scenary);
                float top = 0.6f;
                if (crate != null) { var rs = crate.GetComponentsInChildren<Renderer>(); if (rs.Length > 0) { Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); top = bb.max.y - at.y; } }
                string prop = valuable[audience % valuable.Length];
                Quaternion rot = prop == "Sword" ? Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(0f, audience * 67f, 0f);
                var go = Place("Assets/Prefabs/Audience_Objects/" + prop + ".prefab", root, at + Vector3.up * (top + (prop == "Sword" ? 0.03f : 0.0f)), rot);
                if (go != null) audience++;
            }

            // A framed portrait in the Bilheteria corridors: the foyer's showpiece (150 audience).
            WRoom bil = l.Hub(RoomKind.Bilheteria);
            foreach (var d in bil.DoorPoints.Values.Where(p => Mathf.Abs(p.z - bil.Pivot.z - 2f) < 0.5f))
            {
                Vector3 outward = new Vector3(Mathf.Sign(d.x - bil.Pivot.x), 0f, 0f);
                Vector3 at = d + outward * 4f + Vector3.forward * (ClearWidth * 0.5f - 0.05f) + Vector3.up * 0.6f;
                var go = Place("Assets/Prefabs/Audience_Objects/SM_Sophisticated_Frame.prefab", root, at, Quaternion.LookRotation(Vector3.back, Vector3.up));
                if (go != null) audience++;
            }

            MarkStatic(root.gameObject);
            ctx.Report.AppendLine($"  decoração: {art} quadros/cartazes, {benches} bancos, {boxes} caixas, {carpets} tapetes, {signs} placas de saída, {audience} objetos de audiência");
        }

        private static readonly Dictionary<string, string> PropPaths = new Dictionary<string, string>();

        private static string FindProp(string name)
        {
            if (PropPaths.TryGetValue(name, out string p)) return p;
            string guid = UnityEditor.AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/Prefabs" })
                .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(UnityEditor.AssetDatabase.GUIDToAssetPath(g)) == name);
            p = guid != null ? UnityEditor.AssetDatabase.GUIDToAssetPath(guid) : null;
            PropPaths[name] = p;
            return p;
        }
    }
}
