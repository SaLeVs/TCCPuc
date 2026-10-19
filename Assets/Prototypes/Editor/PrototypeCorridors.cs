using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// Builds the corridors of a laid-out level: flat corridors on both floors as one union per
    /// floor (walls only on the outline, so T, L and X crossings come out right), stairs where the
    /// upper floor meets the ground floor, crouch ducts, mid-run doors, ceiling lights.
    /// </summary>
    public class PrototypeCorridors
    {
        public class Stair { public Vector3 Top, Bottom; public Vector3 Dir; public float Length; public int Steps; }
        public class Duct { public WPiece Piece; public Vector3 A, B; public float FloorA, FloorB; public bool Tunnel; }

        private readonly PrototypeLayout _l;
        private readonly Transform _root;
        public readonly List<GameObject> Sabotage = new List<GameObject>();
        public readonly List<Stair> Stairs = new List<Stair>();
        public readonly List<Duct> Ducts = new List<Duct>();
        public readonly List<Vector3> LightPoints = new List<Vector3>();
        public readonly List<(Vector3 pos, Vector3 along)> DoorSpots = new List<(Vector3, Vector3)>();
        public readonly List<string> Problems = new List<string>();

        // Flat corridor rectangles per floor height, and the points where a wall must stay open.
        private readonly Dictionary<float, List<Rect>> _rects = new Dictionary<float, List<Rect>>();
        private readonly List<(Vector3 point, float width, float top)> _openings = new List<(Vector3, float, float)>();
        private readonly Dictionary<WPiece, (float a, float b)> _flat = new Dictionary<WPiece, (float, float)>();

        private const float StepRise = 0.2045f;  // 22 risers for 4.5 m: the player climbs 0.30 m, the monster 0.50 m
        private const float StepTread = 0.3f;
        private const float SteepRise = 0.28f, SteepTread = 0.28f;

        public PrototypeCorridors(PrototypeLayout layout, Transform root)
        {
            _l = layout;
            _root = root;
        }

        /// <summary>Works out stairs and ducts without building anything (used for camera shots too).</summary>
        public void Plan()
        {
            if (_planned) return;
            _planned = true;
            foreach (WPiece p in _l.Pieces.Where(p => p.Color != BarColor.Yellow)) _flat[p] = (p.A, p.B);
            PlanStairs();
            PlanDucts();
        }

        private bool _planned;

        public void Build()
        {
            Plan();
            CollectRects();
            CollectOpenings();
            BuildFloors();
            BuildHubCaps();
            BuildStairs();
            BuildDucts();
            BuildDoors();
            BuildLights();
        }

        // ------------------------------------------------------------------ stairs

        private static Vector3 Dir(WPiece p) => p.Horizontal ? Vector3.right : Vector3.forward;
        private static Vector3 At(WPiece p, float t, float y) => p.Horizontal ? new Vector3(t, y, p.Line) : new Vector3(p.Line, y, t);

        private void PlanStairs()
        {
            foreach (WPiece g in _l.Pieces.Where(p => p.Color == BarColor.Green))
            {
                // Green meets red: a corridor of the other floor touching it.
                foreach (WPiece r in _l.Pieces.Where(p => p.Color == BarColor.Red && g.Bar.Touching.Contains(p.Bar)))
                    AddStair(g, r, null);

                // Green into a ground-floor room other than the auditorium (whose rear doors are upstairs).
                foreach (var (room, _) in g.Bar.Rooms)
                {
                    if (room.Upper || room.Kind == RoomKind.Auditorium) continue;
                    AddStair(g, null, room);
                }
            }

            // Red into an upper-floor room.
            foreach (WPiece r in _l.Pieces.Where(p => p.Color == BarColor.Red))
            foreach (var (room, _) in r.Bar.Rooms.Where(x => x.room.Upper))
                AddStairUp(r, room);
        }

        private void AddStair(WPiece g, WPiece r, DRoom room)
        {
            // Junction point on the green line, and which end of the green piece it is.
            float j;
            if (r != null) j = r.Horizontal == g.Horizontal ? (Mathf.Abs(r.A - g.B) < Mathf.Abs(r.B - g.A) ? g.B : g.A) : r.Line;
            else
            {
                var w = _l.Rooms.First(x => x.Room == room);
                var p = w.DoorPoints[room.Doors.First(d => d.Bar == g.Bar)];
                j = g.Horizontal ? p.x : p.z;
            }
            bool atB = Mathf.Abs(j - g.B) < Mathf.Abs(j - g.A);
            float margin = r != null && r.Horizontal != g.Horizontal ? ClearWidth * 0.5f + 0.05f : 0f;

            float available = g.Length - margin - 0.5f;
            float rise = StepRise, tread = StepTread;
            int steps = Mathf.CeilToInt(PrototypeLayout.UpperFloor / rise - 0.001f);
            float length = steps * tread;
            if (length > available)
            {
                rise = SteepRise; tread = SteepTread;
                steps = Mathf.CeilToInt(PrototypeLayout.UpperFloor / rise - 0.001f);
                length = steps * tread;
            }
            if (length > available)
            {
                // Not enough green to descend in: continue into the red piece if it carries straight on.
                if (r != null && r.Horizontal == g.Horizontal && r.Length > length + 0.5f) { AddStairInRed(g, r, atB); return; }
                Problems.Add($"escada sem espaço ({available:F1} m disponíveis, {length:F1} m precisos) em {g}");
                return;
            }

            float bottomT = atB ? j - margin : j + margin;
            float topT = atB ? bottomT - length : bottomT + length;
            var (fa, fb) = _flat[g];
            _flat[g] = atB ? (fa, Mathf.Min(fb, topT)) : (Mathf.Max(fa, topT), fb);

            Vector3 dir = Dir(g) * (atB ? 1f : -1f);
            Stairs.Add(new Stair { Top = At(g, topT, PrototypeLayout.UpperFloor), Bottom = At(g, bottomT, 0f), Dir = dir, Length = length, Steps = steps });
        }

        private void AddStairInRed(WPiece g, WPiece r, bool greenEndsAtB)
        {
            int steps = Mathf.CeilToInt(PrototypeLayout.UpperFloor / StepRise - 0.001f);
            float length = steps * StepTread;
            // The red piece starts where the green ends; the stairs run down it from there.
            bool redStartsAtA = Mathf.Abs(r.A - (greenEndsAtB ? g.B : g.A)) < Mathf.Abs(r.B - (greenEndsAtB ? g.B : g.A));
            float topT = redStartsAtA ? r.A : r.B;
            float bottomT = redStartsAtA ? r.A + length : r.B - length;
            var (fa, fb) = _flat[r];
            _flat[r] = redStartsAtA ? (Mathf.Max(fa, bottomT), fb) : (fa, Mathf.Min(fb, bottomT));
            Vector3 dir = Dir(r) * (redStartsAtA ? 1f : -1f);
            Stairs.Add(new Stair { Top = At(r, topT, PrototypeLayout.UpperFloor), Bottom = At(r, bottomT, 0f), Dir = dir, Length = length, Steps = steps });
        }

        private void AddStairUp(WPiece r, DRoom room)
        {
            var w = _l.Rooms.First(x => x.Room == room);
            var p = w.DoorPoints[room.Doors.First(d => d.Bar == r.Bar)];
            float j = r.Horizontal ? p.x : p.z;
            bool atB = Mathf.Abs(j - r.B) < Mathf.Abs(j - r.A);
            int steps = Mathf.CeilToInt(PrototypeLayout.UpperFloor / StepRise - 0.001f);
            float length = steps * StepTread;
            if (length > r.Length - 0.5f) { Problems.Add($"escada sem espaço até sala de cima em {r}"); return; }
            float topT = j;
            float bottomT = atB ? j - length : j + length;
            var (fa, fb) = _flat[r];
            _flat[r] = atB ? (fa, Mathf.Min(fb, bottomT)) : (Mathf.Max(fa, bottomT), fb);
            Vector3 dir = Dir(r) * (atB ? -1f : 1f);
            Stairs.Add(new Stair { Top = At(r, topT, PrototypeLayout.UpperFloor), Bottom = At(r, bottomT, 0f), Dir = dir, Length = length, Steps = steps });
        }

        // ------------------------------------------------------------------ ducts

        private void PlanDucts()
        {
            foreach (WPiece y in _l.Pieces.Where(p => p.Color == BarColor.Yellow))
            {
                var d = new Duct { Piece = y, A = y.PA, B = y.PB, FloorA = y.FloorA, FloorB = y.FloorB };
                // Clip each end to the wall of the corridor it opens into.
                foreach (WPiece c in _l.Pieces.Where(p => p.Color != BarColor.Yellow && y.Bar.Touching.Contains(p.Bar)))
                {
                    if (c.Horizontal == y.Horizontal) continue;
                    float edge = ClearWidth * 0.5f;
                    if (Mathf.Abs(c.Line - y.A) < Mathf.Abs(c.Line - y.B)) d.A = At(y, c.Line + edge, d.FloorA);
                    else d.B = At(y, c.Line - edge, d.FloorB);
                }
                float len = y.Horizontal ? d.B.x - d.A.x : d.B.z - d.A.z;
                d.Tunnel = len > 14f;
                Ducts.Add(d);
            }
        }

        // ------------------------------------------------------------------ flat corridors

        private void CollectRects()
        {
            foreach (var kv in _flat)
            {
                WPiece p = kv.Key;
                float a = kv.Value.a, b = kv.Value.b;
                if (b - a < 0.05f) continue;
                float y = p.FloorA;
                float half = ClearWidth * 0.5f;
                Rect r = p.Horizontal ? Rect.MinMaxRect(a, p.Line - half, b, p.Line + half) : Rect.MinMaxRect(p.Line - half, a, p.Line + half, b);
                if (!_rects.TryGetValue(y, out var list)) _rects[y] = list = new List<Rect>();
                list.Add(r);
            }
        }

        private void CollectOpenings()
        {
            foreach (WRoom w in _l.Rooms)
                foreach (Vector3 p in w.DoorPoints.Values) _openings.Add((p, ClearWidth + 0.1f, 99f));
            foreach (Stair s in Stairs)
            {
                _openings.Add((s.Top, ClearWidth + 0.1f, 99f));
                _openings.Add((s.Bottom, ClearWidth + 0.1f, 99f));
            }
            foreach (Duct d in Ducts)
            {
                _openings.Add((d.A, DuctWidth, DuctHeight));
                _openings.Add((d.B, DuctWidth, DuctHeight));
            }
        }

        private Material FloorMat => MaterialOf(Hallway + "SM_Unique_Floor_Hallway.prefab");
        private Material CeilingMat => MaterialOf(Hallway + "Walls_To_Mount/Roof.prefab") ?? MaterialOf("Assets/Prefabs/Rooms/Props/Hallway/SM_Roof_2.prefab");

        private Material RoofMaterial()
        {
            var roof = Prefab(Hallway + "SM_Roof_2.prefab");
            var mr = roof != null ? roof.GetComponentInChildren<MeshRenderer>(true) : null;
            return mr != null ? mr.sharedMaterial : FloorMat;
        }

        private void BuildFloors()
        {
            var group = Group("Corredores", _root).transform;
            int seed = 0;
            foreach (var kv in _rects)
            {
                float y = kv.Key;
                var union = new RectUnion(kv.Value);
                var cells = union.Cells().ToList();
                string floorName = y > 1f ? "Andar2" : "Terreo";
                var g = Group("Corredores_" + floorName, group).transform;

                MeshObject("Piso", g, FlatMesh(cells, y, true, 1f, "PisoCorredor_" + floorName), FloorMat, Ground);
                MeshObject("Teto", g, FlatMesh(cells, y + WallHeight, false, 1f, "TetoCorredor_" + floorName), RoofMaterial(), Scenary);
                foreach (Rect c in cells)
                    BoxColliderAt("Col_Piso", g, new Vector3(c.center.x, y - 0.1f, c.center.y), new Vector3(c.width, 0.2f, c.height), Ground);

                var walls = Group("Paredes", g).transform;
                foreach (var (start, dir, length, inward) in union.Boundary())
                {
                    var openings = new List<Opening>();
                    foreach (var (point, width, top) in _openings)
                    {
                        if (Mathf.Abs(point.y - y) > 1f && top > 50f) continue;
                        if (top < 50f && Mathf.Abs(point.y - y) > 1f) continue;
                        Vector3 rel = point - start; rel.y = 0;
                        float along = Vector3.Dot(rel, dir);
                        float off = Vector3.Dot(rel, inward);
                        if (Mathf.Abs(off) > 0.3f || along < -width * 0.5f || along > length + width * 0.5f) continue;
                        openings.Add(new Opening { From = along - width * 0.5f, To = along + width * 0.5f, Top = Mathf.Min(top, WallHeight) });
                    }
                    Wall(walls, start, dir, length, y, WallHeight, openings, seed++);
                }
                MarkStatic(g.gameObject);
            }
        }

        // ------------------------------------------------------------------ hub door caps

        /// <summary>Width of the opening each hub room has at a door, as built in its prefab.</summary>
        private static float HubDoorWidth(RoomKind kind, Side side)
        {
            switch (kind)
            {
                case RoomKind.Auditorium: return side == Side.Left || side == Side.Right ? 2.2f : 2.0f;
                case RoomKind.Bilheteria: return side == Side.Left || side == Side.Right ? 1.4f : 2.6f;
                case RoomKind.Escape: return 2.0f;
                case RoomKind.Dressing: return 2.0f;
                default: return 1.2f;
            }
        }

        /// <summary>
        /// Puzzle rooms end in a door stub exactly as wide as the corridor; the hub rooms do not.
        /// Close the corridor end at their doors with a wall that keeps only their opening.
        /// </summary>
        private void BuildHubCaps()
        {
            var group = Group("TampasPortasCentro", _root).transform;
            foreach (WRoom w in _l.Rooms.Where(r => r.Room.Kind != RoomKind.Puzzle))
            {
                foreach (var kv in w.DoorPoints)
                {
                    WPiece piece = _l.Pieces.FirstOrDefault(p => p.Bar == kv.Key.Bar);
                    if (piece == null || piece.Color == BarColor.Yellow) continue;
                    Vector3 p = kv.Value;
                    float t = piece.Horizontal ? p.x : p.z;
                    Vector3 inward = Dir(piece) * (Mathf.Abs(t - piece.A) < Mathf.Abs(t - piece.B) ? 1f : -1f);
                    Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 start = new Vector3(p.x, 0f, p.z) - dir * (ClearWidth * 0.5f);
                    float width = HubDoorWidth(w.Room.Kind, kv.Key.Side);
                    var hole = new List<Opening> { new Opening { From = (ClearWidth - width) * 0.5f, To = (ClearWidth + width) * 0.5f, Top = 2.35f } };
                    Wall(group, start, dir, ClearWidth, p.y, WallHeight, hole, 3);
                }
            }
            MarkStatic(group.gameObject);
        }

        // ------------------------------------------------------------------ stairs geometry

        private void BuildStairs()
        {
            if (Stairs.Count == 0) return;
            var group = Group("Escadas", _root).transform;
            int seed = 0;
            foreach (Stair s in Stairs)
            {
                var g = Group("Escada", group).transform;
                float rise = PrototypeLayout.UpperFloor / s.Steps;
                float tread = s.Length / s.Steps;
                Vector3 side = Vector3.Cross(Vector3.up, s.Dir).normalized;

                // Solid steps: each block from the ground up to its tread (nothing hollow to see under).
                for (int i = 0; i < s.Steps; i++)
                {
                    float topY = PrototypeLayout.UpperFloor - i * rise;
                    Vector3 center = s.Top + s.Dir * (tread * (i + 0.5f));
                    center.y = topY * 0.5f;
                    Vector3 size = Mathf.Abs(s.Dir.x) > 0.5f ? new Vector3(tread, topY, ClearWidth) : new Vector3(ClearWidth, topY, tread);
                    MeshObject("Degrau", g, BoxMesh(size, 1f), FloorMat, Ground, center);
                    BoxColliderAt("Col_Degrau", g, center, size, Ground);
                }

                // Side walls from the ground to the upper ceiling.
                float top = PrototypeLayout.UpperFloor + WallHeight;
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector3 inward = -side * sgn;
                    Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 edgeA = s.Top + side * sgn * (ClearWidth * 0.5f), edgeB = s.Bottom + side * sgn * (ClearWidth * 0.5f);
                    Vector3 start = Vector3.Dot(edgeB - edgeA, dir) > 0 ? edgeA : edgeB;
                    start.y = 0;
                    Wall(g, start, dir, s.Length, 0f, top, null, seed++);
                }

                // Over the bottom end, from the lower ceiling up to the stair ceiling.
                {
                    Vector3 inward = -s.Dir;
                    Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 a = s.Bottom - side * (ClearWidth * 0.5f), b = s.Bottom + side * (ClearWidth * 0.5f);
                    Vector3 start = Vector3.Dot(b - a, dir) > 0 ? a : b;
                    start.y = 0;
                    Wall(g, start, dir, ClearWidth, WallHeight, top - WallHeight, null, seed++);
                }

                // Ceiling over the whole stairwell, and a lamp halfway down.
                Vector3 mid = (s.Top + s.Bottom) * 0.5f;
                Rect r = Mathf.Abs(s.Dir.x) > 0.5f
                    ? Rect.MinMaxRect(Mathf.Min(s.Top.x, s.Bottom.x), mid.z - ClearWidth * 0.5f, Mathf.Max(s.Top.x, s.Bottom.x), mid.z + ClearWidth * 0.5f)
                    : Rect.MinMaxRect(mid.x - ClearWidth * 0.5f, Mathf.Min(s.Top.z, s.Bottom.z), mid.x + ClearWidth * 0.5f, Mathf.Max(s.Top.z, s.Bottom.z));
                MeshObject("Teto", g, FlatMesh(new[] { r }, top, false, 1f, "TetoEscada"), RoofMaterial(), Scenary);
                CeilingLight(g, new Vector3(mid.x, 0f, mid.z), top, new Color(1f, 0.8f, 0.58f), 4.8f, 11f, true, Sabotage);
                MarkStatic(g.gameObject);
            }
        }

        // ------------------------------------------------------------------ ducts geometry

        private Material _ductMat, _grateMat;

        private Material DuctMaterial()
        {
            if (_ductMat != null) return _ductMat;
            string path = PrototypeAssets.MaterialsFolder + "/M_Duto.mat";
            _ductMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (_ductMat == null)
            {
                _ductMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Duto" };
                _ductMat.SetColor("_BaseColor", new Color(0.42f, 0.44f, 0.46f));
                _ductMat.SetFloat("_Metallic", 0.85f);
                _ductMat.SetFloat("_Smoothness", 0.38f);
                UnityEditor.AssetDatabase.CreateAsset(_ductMat, path);
            }
            return _ductMat;
        }

        private Material GrateMaterial()
        {
            if (_grateMat != null) return _grateMat;
            string path = PrototypeAssets.MaterialsFolder + "/M_Duto_Grade.mat";
            _grateMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (_grateMat == null)
            {
                _grateMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Duto_Grade" };
                _grateMat.SetColor("_BaseColor", new Color(0.18f, 0.19f, 0.2f));
                _grateMat.SetFloat("_Metallic", 0.9f);
                _grateMat.SetFloat("_Smoothness", 0.25f);
                UnityEditor.AssetDatabase.CreateAsset(_grateMat, path);
            }
            return _grateMat;
        }

        private void BuildDucts()
        {
            if (Ducts.Count == 0) return;
            var group = Group("Dutos", _root).transform;
            foreach (Duct d in Ducts)
            {
                var g = Group(d.Tunnel ? "TunelManutencao" : "Duto", group).transform;
                Vector3 a = d.A, b = d.B;
                Vector3 dir = (new Vector3(b.x, 0, b.z) - new Vector3(a.x, 0, a.z)).normalized;
                float len = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z));
                bool drop = Mathf.Abs(d.FloorA - d.FloorB) > 0.5f;

                // Segments along the duct: low crawl sections at the mouths; a taller tunnel between when long.
                var segs = new List<(float t0, float t1, float h)>();
                if (d.Tunnel) { segs.Add((0f, 2.5f, DuctHeight)); segs.Add((2.5f, len - 2.5f, 2.3f)); segs.Add((len - 2.5f, len, DuctHeight)); }
                else segs.Add((0f, len, DuctHeight));

                float FloorAt(float t)
                {
                    if (!drop) return d.FloorA;
                    // Steps of at most 0.3 m, spread over the middle of the duct.
                    float dropLen = Mathf.Max(len - 2f, 1f);
                    int steps = Mathf.CeilToInt(Mathf.Abs(d.FloorA - d.FloorB) / 0.29f);
                    float tread = Mathf.Min(dropLen / steps, 0.9f);
                    float startT = (len - tread * steps) * 0.5f;
                    int k = Mathf.Clamp(Mathf.FloorToInt((t - startT) / tread) + 1, 0, steps);
                    return Mathf.Lerp(d.FloorA, d.FloorB, (float)k / steps);
                }

                float w = d.Tunnel ? 1.6f : DuctWidth;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float step = drop ? 0.3f : 1f;
                foreach (var (t0, t1, h) in segs)
                {
                    for (float t = t0; t < t1 - 0.001f; t += step)
                    {
                        float te = Mathf.Min(t + step, t1);
                        float fy = FloorAt((t + te) * 0.5f);
                        float l = te - t;
                        Vector3 c = new Vector3(a.x, 0, a.z) + dir * ((t + te) * 0.5f);
                        Vector3 sizeAlong(float sx, float sy, float sw) => Mathf.Abs(dir.x) > 0.5f ? new Vector3(sx, sy, sw) : new Vector3(sw, sy, sx);
                        // floor block (solid down to the lower floor so steps are closed), ceiling, sides
                        float baseY = Mathf.Min(d.FloorA, d.FloorB);
                        var floorSize = sizeAlong(l, Mathf.Max(0.1f, fy - baseY + 0.1f), w);
                        MeshObject("Piso", g, BoxMesh(floorSize, 1f), GrateMaterial(), Ground, c + Vector3.up * (baseY + floorSize.y * 0.5f - 0.1f));
                        BoxColliderAt("Col_Piso", g, c + Vector3.up * (baseY + floorSize.y * 0.5f - 0.1f), floorSize, Ground);
                        var roof = sizeAlong(l, 0.1f, w + 0.2f);
                        MeshObject("Teto", g, BoxMesh(roof, 1f), DuctMaterial(), Walls, c + Vector3.up * (fy + h + 0.05f));
                        BoxColliderAt("Col_Teto", g, c + Vector3.up * (fy + h + 0.05f), roof, Walls);
                        foreach (float sgn in new[] { -1f, 1f })
                        {
                            var wall = sizeAlong(l, h + 0.2f, 0.1f);
                            Vector3 wc = c + side * sgn * (w * 0.5f + 0.05f) + Vector3.up * (fy + h * 0.5f);
                            MeshObject("Lado", g, BoxMesh(wall, 1f), DuctMaterial(), Walls, wc);
                            BoxColliderAt("Col_Lado", g, wc, wall, Walls);
                        }
                    }
                }

                // A duct that starts at a room door: close the rest of the door stub around its mouth.
                foreach (var (end, floor) in new[] { (a, d.FloorA), (b, d.FloorB) })
                {
                    bool atDoor = _l.Rooms.Any(rw => rw.DoorPoints.Values.Any(p => Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(end.x, 0, end.z)) < 0.6f));
                    if (!atDoor) continue;
                    Vector3 outward = end == a ? -dir : dir;          // towards the room
                    Vector3 inward = -outward;                          // the duct side is the inside of this panel
                    Vector3 wdir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 start = new Vector3(end.x, 0, end.z) - wdir * (ClearWidth * 0.5f);
                    var hole = new List<Opening> { new Opening { From = ClearWidth * 0.5f - w * 0.5f, To = ClearWidth * 0.5f + w * 0.5f, Top = DuctHeight } };
                    Wall(g, start, wdir, ClearWidth, floor, WallHeight, hole, 0);
                }

                // Dim emergency light inside long tunnels.
                if (d.Tunnel)
                {
                    Vector3 m = (new Vector3(a.x, 0, a.z) + new Vector3(b.x, 0, b.z)) * 0.5f;
                    var lgo = Place("Assets/Prefabs/Managers/SabotageObjects/SabotageLight.prefab", g, m + Vector3.up * (FloorAt(len * 0.5f) + 2.1f), Quaternion.identity);
                    if (lgo != null) { var lt = lgo.GetComponent<Light>(); lt.color = new Color(1f, 0.35f, 0.25f); lt.intensity = 1.6f; lt.range = 8f; lt.shadows = LightShadows.None; Sabotage.Add(lgo); }
                }
                MarkStatic(g.gameObject);
            }
        }

        // ------------------------------------------------------------------ doors in long runs

        private void BuildDoors()
        {
            var group = Group("PortasCorredor", _root).transform;
            foreach (var kv in _flat)
            {
                WPiece p = kv.Key;
                float a = kv.Value.a, b = kv.Value.b;
                // Crossings along this piece split it into runs.
                var cuts = new List<float> { a, b };
                foreach (WPiece o in _l.Pieces.Where(o => o != p && o.Horizontal != p.Horizontal && p.Bar.Touching.Contains(o.Bar)))
                    if (o.Line > a + 0.5f && o.Line < b - 0.5f) cuts.Add(o.Line);
                cuts.Sort();
                for (int i = 1; i < cuts.Count; i++)
                {
                    float run = cuts[i] - cuts[i - 1];
                    if (run < 24f) continue;
                    float t = (cuts[i] + cuts[i - 1]) * 0.5f;
                    Vector3 pos = At(p, t, p.FloorA);
                    Vector3 along = Dir(p);
                    PlaceCorridorDoor(group, pos, along);
                    DoorSpots.Add((pos, along));
                }
            }
        }

        private void PlaceCorridorDoor(Transform parent, Vector3 pos, Vector3 along)
        {
            // Wall with a double-door opening across the corridor, and the networked double door in it.
            Vector3 across = Vector3.Cross(Vector3.up, along).normalized;
            float width = ClearWidth + WallThickness * 2f;
            Quaternion wallRot = Quaternion.LookRotation(across, Vector3.up); // piece runs along +z
            Vector3 start = pos - across * (width * 0.5f) + along * (WallThickness * 0.5f);
            var wall = Place(Hallway + "SM_Sophisticated_Wall_Double_Door_4m_V1.prefab", parent, start, wallRot, new Vector3(1f, 1f, width / 4f));
            if (wall != null) SetLayerRecursive(wall, Walls);
            var door = Place("Assets/Prefabs/Rooms/Props/Doors/Door_Double_Black.prefab", parent, pos, Quaternion.LookRotation(along, Vector3.up));
            if (door != null) Sabotage.Add(door);
        }

        // ------------------------------------------------------------------ lights

        private void BuildLights()
        {
            var group = Group("LuzesCorredor", _root).transform;
            var rnd = new System.Random(_l.Pieces.Count * 31 + _l.Rooms.Count);
            var warm = new Color(1f, 0.79f, 0.56f);
            foreach (var kv in _flat)
            {
                WPiece p = kv.Key;
                float a = kv.Value.a, b = kv.Value.b;
                float len = b - a;
                if (len < 3f) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 7.5f));
                for (int i = 0; i < n; i++)
                {
                    float t = a + len * (i + 0.5f) / n;
                    Vector3 pos = At(p, t, p.FloorA);
                    if (LightPoints.Any(q => Vector3.Distance(q, pos) < 4f)) continue;
                    bool on = rnd.NextDouble() > 0.12; // a few dead lamps: darker stretches to break the rhythm
                    CeilingLight(group, new Vector3(pos.x, 0f, pos.z), p.FloorA + WallHeight, warm, 5.2f, 10f, on, Sabotage);
                    LightPoints.Add(pos);
                }

                // Wall sconces halfway between the ceiling lamps, alternating sides: warm pools on the
                // wainscot that keep the corridor readable without flattening it.
                for (int i = 0; i < n - 1 || (n == 1 && len > 9f && i < 1); i++)
                {
                    float t = n == 1 ? a + len * 0.25f : a + len * (i + 1f) / n;
                    Vector3 pos = At(p, t, p.FloorA);
                    if (_openings.Any(o => Mathf.Abs(o.point.y - p.FloorA) < 1.5f && Vector2.Distance(new Vector2(o.point.x, o.point.z), new Vector2(pos.x, pos.z)) < 3.2f)) continue;
                    if (_l.Pieces.Any(o => o != p && o.Horizontal != p.Horizontal && p.Bar.Touching.Contains(o.Bar) && Mathf.Abs(o.Line - t) < 3.2f)) continue;
                    float side = (i + (p.Horizontal ? 0 : 1)) % 2 == 0 ? 1f : -1f;
                    Vector3 across = p.Horizontal ? Vector3.forward : Vector3.right;
                    Vector3 inward = -across * side;
                    Vector3 wall = pos + across * side * (ClearWidth * 0.5f) + Vector3.up * 2.05f;
                    var sconce = Place(Hallway + "SM_Single_Light.prefab", group, wall, Quaternion.LookRotation(Vector3.Cross(Vector3.up, inward), Vector3.up));
                    if (sconce == null) continue;
                    SetLayerRecursive(sconce, PrototypeGeometry.Props);
                    var lgo = Place("Assets/Prefabs/Managers/SabotageObjects/SabotageLight.prefab", sconce.transform, wall + inward * 0.45f - Vector3.up * 0.1f, Quaternion.identity);
                    if (lgo == null) continue;
                    var l = lgo.GetComponent<Light>();
                    l.type = LightType.Point; l.color = new Color(1f, 0.74f, 0.48f); l.intensity = 2.3f; l.range = 6f;
                    l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Mixed;
                    Sabotage.Add(lgo);
                }
            }
        }
    }
}
