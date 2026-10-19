using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// Builds the corridors of a laid-out level: flat corridors on both floors as one union per
    /// floor (walls only on the outline, so T, L and X crossings come out right), the Bilheteria as
    /// part of that same outline, stairs where the upper floor meets the ground floor, the backstage
    /// steps, ducts, mid-run doors and lights. Each wing has its own wallpaper and floor so the
    /// player can tell where they are.
    /// </summary>
    public class PrototypeCorridors
    {
        public class Stair { public Vector3 Top, Bottom, Dir; public float Length, Landing; public int Steps; public bool Exit = true; }
        public class Duct { public WPiece Piece; public Vector3 A, B; public float FloorA, FloorB; public bool Tunnel; }

        /// <summary>A hub room built from the corridor outline instead of its prefab walls: the corridors run into it with no seam.</summary>
        public class Shell { public WRoom Room; public Rect Area; public float Y, Height; public Material Paper, Floor; public float FloorUv; }

        private readonly PrototypeLayout _l;
        private readonly Transform _root;
        public readonly List<GameObject> Sabotage = new List<GameObject>();
        public readonly List<Stair> Stairs = new List<Stair>();
        public readonly List<Duct> Ducts = new List<Duct>();
        public readonly List<Shell> Shells = new List<Shell>();
        public readonly List<Vector3> LightPoints = new List<Vector3>();
        public readonly List<Vector3> WallFixtures = new List<Vector3>();
        public readonly List<(Vector3 pos, Vector3 along)> DoorSpots = new List<(Vector3, Vector3)>();
        public readonly List<string> Problems = new List<string>();

        // Flat corridor rectangles per floor height, and the points where a wall must stay open.
        private readonly Dictionary<float, List<Rect>> _rects = new Dictionary<float, List<Rect>>();
        private readonly Dictionary<Rect, int> _rectZone = new Dictionary<Rect, int>(); // each corridor keeps the look of the wing it belongs to
        private readonly List<(Vector3 point, float width, float top)> _openings = new List<(Vector3, float, float)>();
        private readonly Dictionary<WPiece, (float a, float b)> _flat = new Dictionary<WPiece, (float, float)>();
        public IReadOnlyDictionary<WPiece, (float a, float b)> Flat => _flat;

        private const float StepRise = 0.2045f;  // 22 risers for 4.5 m: the player climbs 0.30 m, the monster 0.50 m
        private const float StepTread = 0.3f;
        private const float SteepRise = 0.28f, SteepTread = 0.28f;
        public const float StageFloor = 1.5f;
        private const float StageBackFace = -15.58f; // stage side of the stage's back wall (auditorium space)

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
            PlanShells();
            PlanStairs();
            PlanBackstage();
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

        // ------------------------------------------------------------------ zones

        private const string Mats = "Assets/Materials/Scenary/";

        /// <summary>0 centre, 1 west wing, 2 east wing, 3 far ring.</summary>
        public static int ZoneOf(Vector3 p) => Mathf.Abs(p.x) >= 70f ? 3 : p.x <= -20f ? 1 : p.x >= 20f ? 2 : 0;

        private static readonly string[] GroundPapers = { null, Mats + "Walls/V7/M_WallV7.mat", Mats + "Walls/V9/M_WallV9.mat", Mats + "Hallway/Walls/V1/M_WallHallway_1.mat" };
        private static readonly string[] UpperPapers = { Mats + "Dressing_room/Wall/V2/M_Wall_2.mat", Mats + "Walls/V5/M_WallV5.mat", Mats + "Walls/V6/M_WallV7.mat", Mats + "Dressing_room/Wall/V2/M_Wall_2.mat" };
        private static readonly (string mat, float uv)[] GroundFloors =
        {
            (Mats + "Hallway/Ground/M_GroundHallway.mat", 1f), (Mats + "Floor/V2/M_FloorV2.mat", 0.5f),
            (Mats + "Dressing_room/Floor/V1/M_Floor_1.mat", 1f), (Mats + "Floor/V10/M_FloorV10.mat", 0.33f),
        };
        private static readonly (string mat, float uv)[] UpperFloors =
        {
            (Mats + "Hallway/Ground/M_GroundHallway.mat", 1f), (Mats + "Floor/V2/M_FloorV2.mat", 0.5f),
            (Mats + "Floor/V2/M_FloorV2.mat", 0.5f), (Mats + "Hallway/Ground/M_GroundHallway.mat", 1f),
        };
        private const string StairPaper = Mats + "Hallway/Walls/V1/M_WallHallway_1.mat";

        public static Material PaperAt(Vector3 p)
        {
            string path = (p.y > 1f ? UpperPapers : GroundPapers)[ZoneOf(p)];
            return path != null ? Mat(path) : null;
        }

        // ------------------------------------------------------------------ shells

        private void PlanShells()
        {
            WRoom bil = _l.Rooms.FirstOrDefault(r => r.Room.Kind == RoomKind.Bilheteria);
            if (bil == null) return;
            Shells.Add(new Shell
            {
                Room = bil, Area = bil.Footprint, Y = bil.Pivot.y, Height = 4f, FloorUv = 0.25f,
                Paper = Mat(Mats + "Box_Office/Wall/V1/M_WallBoxOffice.mat"), Floor = Mat(Mats + "Box_Office/Floor/M_FloorBoxOffice.mat"),
            });
        }

        private bool IsShell(WRoom w) => Shells.Any(s => s.Room == w);

        // ------------------------------------------------------------------ stairs

        private static Vector3 Dir(WPiece p) => p.Horizontal ? Vector3.right : Vector3.forward;
        private static Vector3 At(WPiece p, float t, float y) => p.Horizontal ? new Vector3(t, y, p.Line) : new Vector3(p.Line, y, t);
        private static float T(WPiece p, Vector3 v) => p.Horizontal ? v.x : v.z;

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
            // Into the side of a crossing corridor: the stairs end exactly at its inner wall face.
            float margin = r != null && r.Horizontal != g.Horizontal ? ClearWidth * 0.5f : 0f;

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

        /// <summary>
        /// The door in the stage's back wall is at stage height (1.5 m): a landing through the wall and
        /// a short flight down to the corridor, walled and roofed like the other stairs.
        /// </summary>
        private void PlanBackstage()
        {
            WRoom aud = _l.Rooms.FirstOrDefault(r => r.Room.Kind == RoomKind.Auditorium);
            if (aud == null) return;
            foreach (var kv in aud.DoorPoints.Where(k => k.Key.Side == Side.Bottom))
            {
                WPiece p = _l.Pieces.FirstOrDefault(x => x.Bar == kv.Key.Bar && x.Color != BarColor.Yellow);
                if (p == null) continue;
                float t = T(p, kv.Value);
                bool atB = Mathf.Abs(t - p.B) < Mathf.Abs(t - p.A);
                Vector3 dir = Dir(p) * (atB ? -1f : 1f);
                int steps = Mathf.CeilToInt(StageFloor / StepRise - 0.001f);
                float length = steps * StepTread, landing = 1.7f;
                Vector3 top = new Vector3(kv.Value.x, StageFloor, aud.Pivot.z + StageBackFace);
                Vector3 bottom = top + dir * (landing + length);
                bottom.y = 0f;
                if (landing + length > p.Length - 1f) { Problems.Add("bastidores: corredor curto demais para a escada do palco"); continue; }
                float bottomT = T(p, bottom);
                var (fa, fb) = _flat[p];
                _flat[p] = atB ? (fa, Mathf.Min(fb, bottomT)) : (Mathf.Max(fa, bottomT), fb);
                Stairs.Add(new Stair { Top = top, Bottom = bottom, Dir = dir, Length = length, Landing = landing, Steps = steps, Exit = false });
            }
        }

        private bool IsBackstageDoor(WRoom w, DDoor d) => w.Room.Kind == RoomKind.Auditorium && d.Side == Side.Bottom && Stairs.Any(s => !s.Exit);

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
                _rectZone[r] = ZoneOf(At(p, (p.A + p.B) * 0.5f, y));
            }
        }

        private void CollectOpenings()
        {
            foreach (WRoom w in _l.Rooms)
            {
                if (IsShell(w)) continue; // the union itself leaves the corridor mouths open
                foreach (var kv in w.DoorPoints)
                {
                    if (IsBackstageDoor(w, kv.Key)) continue;
                    _openings.Add((kv.Value, ClearWidth + 0.1f, 99f));
                }
            }
            // Stairs: 2 cm wider than the stairwell, so the corridor wall ends hide inside the stair walls.
            foreach (Stair s in Stairs)
            {
                _openings.Add((s.Top, ClearWidth + 0.04f, 99f));
                _openings.Add((s.Bottom, ClearWidth + 0.04f, 99f));
            }
            foreach (Duct d in Ducts)
            {
                _openings.Add((d.A, DuctWidth, DuctHeight));
                _openings.Add((d.B, DuctWidth, DuctHeight));
            }
        }

        private Material FloorMat => MaterialOf(Hallway + "SM_Unique_Floor_Hallway.prefab");

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
            var heights = _rects.Keys.Concat(Shells.Select(s => s.Y)).Distinct().ToList();
            foreach (float y in heights)
            {
                var corridors = _rects.TryGetValue(y, out var list) ? list : new List<Rect>();
                var shells = Shells.Where(s => Mathf.Abs(s.Y - y) < 0.01f).ToList();
                string floorName = y > 1f ? "Andar2" : "Terreo";
                var g = Group("Corredores_" + floorName, group).transform;

                // Floors: one mesh per wing, so each wing has its own boards.
                if (corridors.Count > 0)
                {
                    var cells = new RectUnion(corridors).Cells().ToList();
                    var floors = y > 1f ? UpperFloors : GroundFloors;
                    foreach (var zone in cells.GroupBy(c => ZoneOfCell(corridors, c, y)))
                    {
                        var (mat, uv) = floors[zone.Key];
                        MeshObject("Piso_Ala" + zone.Key, g, FlatMesh(zone, y, true, uv, $"PisoCorredor_{floorName}_{zone.Key}"), Mat(mat) ?? FloorMat, Ground);
                    }
                    MeshObject("Teto", g, FlatMesh(cells, y + WallHeight, false, 1f, "TetoCorredor_" + floorName), RoofMaterial(), Scenary);
                    foreach (Rect c in cells)
                        BoxColliderAt("Col_Piso", g, new Vector3(c.center.x, y - 0.1f, c.center.y), new Vector3(c.width, 0.2f, c.height), Ground);
                }
                foreach (Shell s in shells)
                {
                    var sg = Group("Sala_" + s.Room.Room.Kind, g).transform;
                    MeshObject("Piso", sg, FlatMesh(new[] { s.Area }, y, true, s.FloorUv, "PisoSala"), s.Floor ?? FloorMat, Ground);
                    MeshObject("Teto", sg, FlatMesh(new[] { s.Area }, y + s.Height, false, 0.5f, "TetoSala"), RoofMaterial(), Scenary);
                    BoxColliderAt("Col_Piso", sg, new Vector3(s.Area.center.x, y - 0.1f, s.Area.center.y), new Vector3(s.Area.width, 0.2f, s.Area.height), Ground);
                }

                // Walls on the outline of corridors + shells.
                var union = new RectUnion(corridors.Concat(shells.Select(s => s.Area)).ToList());
                var walls = Group("Paredes", g).transform;
                foreach (var (start, dir, length, inward) in union.Boundary(WallThickness))
                {
                    var openings = new List<Opening>();
                    foreach (var (point, width, top) in _openings)
                    {
                        if (Mathf.Abs(point.y - y) > 1f) continue;
                        Vector3 rel = point - start; rel.y = 0;
                        float along = Vector3.Dot(rel, dir);
                        float off = Vector3.Dot(rel, inward);
                        if (Mathf.Abs(off) > 0.3f || along < -width * 0.5f || along > length + width * 0.5f) continue;
                        openings.Add(new Opening { From = along - width * 0.5f, To = along + width * 0.5f, Top = Mathf.Min(top, WallHeight) });
                    }
                    Vector3 mid = start + dir * (length * 0.5f); mid.y = y;
                    Shell sh = shells.FirstOrDefault(s => OnEdge(s.Area, mid));
                    if (sh != null) Wall(walls, start, dir, length, y, sh.Height, openings, seed++, paper: sh.Paper, rowHeight: sh.Height);
                    else Wall(walls, start, dir, length, y, WallHeight, openings, seed++, paper: Paper(ZoneOfRun(corridors, start, dir, length, y), y));
                }

                // Where a corridor (3 m ceiling) runs into a taller shell: close the strip above its mouth.
                foreach (Shell s in shells)
                foreach (Rect c in corridors)
                {
                    if (!Mouth(s.Area, c, out Vector3 a, out Vector3 b, out Vector3 inward)) continue;
                    Vector3 wdir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 start = Vector3.Dot(b - a, wdir) > 0 ? a : b;
                    float len = Vector3.Distance(a, b);
                    Wall(walls, start - wdir * WallThickness, wdir, len + WallThickness * 2f, y + WallHeight, s.Height - WallHeight, null, seed++, paper: s.Paper);
                }
                MarkStatic(g.gameObject);
            }
        }

        private int ZoneOfCell(List<Rect> rects, Rect cell, float y)
        {
            foreach (Rect r in rects) if (r.Contains(cell.center) && _rectZone.TryGetValue(r, out int z)) return z;
            return ZoneOf(new Vector3(cell.center.x, y, cell.center.y));
        }

        /// <summary>The wing of the corridor a wall run belongs to: the rect whose edge it overlaps the most.</summary>
        private int ZoneOfRun(List<Rect> rects, Vector3 start, Vector3 dir, float length, float y)
        {
            Vector3 end = start + dir * length;
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            float a0 = alongX ? Mathf.Min(start.x, end.x) : Mathf.Min(start.z, end.z), a1 = alongX ? Mathf.Max(start.x, end.x) : Mathf.Max(start.z, end.z);
            float line = alongX ? start.z : start.x;
            int best = -1; float bestOverlap = 0.01f;
            foreach (Rect r in rects)
            {
                bool onEdge = alongX ? Mathf.Abs(r.yMin - line) < 0.01f || Mathf.Abs(r.yMax - line) < 0.01f : Mathf.Abs(r.xMin - line) < 0.01f || Mathf.Abs(r.xMax - line) < 0.01f;
                if (!onEdge) continue;
                float overlap = Mathf.Min(a1, alongX ? r.xMax : r.yMax) - Mathf.Max(a0, alongX ? r.xMin : r.yMin);
                if (overlap > bestOverlap && _rectZone.TryGetValue(r, out int z)) { bestOverlap = overlap; best = z; }
            }
            return best >= 0 ? best : ZoneOf((start + end) * 0.5f + Vector3.up * y);
        }

        private static Material Paper(int zone, float y)
        {
            string path = (y > 1f ? UpperPapers : GroundPapers)[zone];
            return path != null ? Mat(path) : null;
        }

        private static bool OnEdge(Rect r, Vector3 p)
        {
            const float e = 0.01f;
            bool inX = p.x > r.xMin - e && p.x < r.xMax + e, inZ = p.z > r.yMin - e && p.z < r.yMax + e;
            return (inZ && (Mathf.Abs(p.x - r.xMin) < e || Mathf.Abs(p.x - r.xMax) < e)) || (inX && (Mathf.Abs(p.z - r.yMin) < e || Mathf.Abs(p.z - r.yMax) < e));
        }

        /// <summary>The stretch where corridor rect c touches the edge of shell area s, and the inward normal of s there.</summary>
        private static bool Mouth(Rect s, Rect c, out Vector3 a, out Vector3 b, out Vector3 inward)
        {
            const float e = 0.01f;
            a = b = inward = Vector3.zero;
            float x0 = Mathf.Max(s.xMin, c.xMin), x1 = Mathf.Min(s.xMax, c.xMax), z0 = Mathf.Max(s.yMin, c.yMin), z1 = Mathf.Min(s.yMax, c.yMax);
            if (Mathf.Abs(c.yMax - s.yMin) < e && x1 - x0 > 0.5f) { a = new Vector3(x0, 0, s.yMin); b = new Vector3(x1, 0, s.yMin); inward = Vector3.forward; return true; }
            if (Mathf.Abs(c.yMin - s.yMax) < e && x1 - x0 > 0.5f) { a = new Vector3(x0, 0, s.yMax); b = new Vector3(x1, 0, s.yMax); inward = Vector3.back; return true; }
            if (Mathf.Abs(c.xMax - s.xMin) < e && z1 - z0 > 0.5f) { a = new Vector3(s.xMin, 0, z0); b = new Vector3(s.xMin, 0, z1); inward = Vector3.right; return true; }
            if (Mathf.Abs(c.xMin - s.xMax) < e && z1 - z0 > 0.5f) { a = new Vector3(s.xMax, 0, z0); b = new Vector3(s.xMax, 0, z1); inward = Vector3.left; return true; }
            return false;
        }

        // ------------------------------------------------------------------ hub door caps

        /// <summary>Width of the opening each hub room has at a door, as built in its prefab.</summary>
        private static float HubDoorWidth(RoomKind kind, Side side)
        {
            switch (kind)
            {
                case RoomKind.Auditorium: return side == Side.Left || side == Side.Right ? 2.2f : 2.0f;
                case RoomKind.Escape: return 2.0f;
                case RoomKind.Dressing: return 2.0f;
                default: return 1.2f;
            }
        }

        /// <summary>
        /// Puzzle rooms end in a door stub exactly as wide as the corridor; the prefab hub rooms do not.
        /// Close the corridor end at their doors with a wall that keeps only their opening. The wall
        /// stands just inside the corridor, its back on the room's outer face, so the two never overlap.
        /// </summary>
        private void BuildHubCaps()
        {
            var group = Group("TampasPortasCentro", _root).transform;
            foreach (WRoom w in _l.Rooms.Where(r => r.Room.Kind != RoomKind.Puzzle && !IsShell(r)))
            {
                foreach (var kv in w.DoorPoints)
                {
                    if (IsBackstageDoor(w, kv.Key)) continue;
                    WPiece piece = _l.Pieces.FirstOrDefault(p => p.Bar == kv.Key.Bar);
                    if (piece == null || piece.Color == BarColor.Yellow) continue;
                    Vector3 p = kv.Value;
                    float t = T(piece, p);
                    Vector3 inward = Dir(piece) * (Mathf.Abs(t - piece.A) < Mathf.Abs(t - piece.B) ? 1f : -1f);
                    Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 start = new Vector3(p.x, 0f, p.z) + inward * WallThickness - dir * (ClearWidth * 0.5f);
                    float width = HubDoorWidth(w.Room.Kind, kv.Key.Side);
                    var hole = new List<Opening> { new Opening { From = (ClearWidth - width) * 0.5f, To = (ClearWidth + width) * 0.5f, Top = 2.35f } };
                    Wall(group, start, dir, ClearWidth, p.y, WallHeight, hole, 3, paper: PaperAt(p));
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
            Material paper = Mat(StairPaper);
            foreach (Stair s in Stairs)
            {
                var g = Group(s.Exit ? "Escada" : "Escada_Bastidores", group).transform;
                float baseY = s.Bottom.y;
                float h = s.Top.y - baseY;
                float rise = h / s.Steps;
                float tread = s.Length / s.Steps;
                float total = s.Landing + s.Length;
                Vector3 side = Vector3.Cross(Vector3.up, s.Dir).normalized;
                bool alongX = Mathf.Abs(s.Dir.x) > 0.5f;
                Vector3 Size(float along, float height) => alongX ? new Vector3(along, height, ClearWidth) : new Vector3(ClearWidth, height, along);

                // Landing at the top, then solid steps, each block from the floor below up to its tread.
                if (s.Landing > 0.01f)
                {
                    Vector3 c = s.Top + s.Dir * (s.Landing * 0.5f); c.y = baseY + h * 0.5f;
                    MeshObject("Patamar", g, BoxMesh(Size(s.Landing, h), 1f), FloorMat, Ground, c);
                    BoxColliderAt("Col_Patamar", g, c, Size(s.Landing, h), Ground);
                }
                for (int i = 0; i < s.Steps; i++)
                {
                    float topY = s.Top.y - i * rise;
                    Vector3 center = s.Top + s.Dir * (s.Landing + tread * (i + 0.5f));
                    center.y = baseY + (topY - baseY) * 0.5f;
                    MeshObject("Degrau", g, BoxMesh(Size(tread, topY - baseY), 1f), FloorMat, Ground, center);
                    BoxColliderAt("Col_Degrau", g, center, Size(tread, topY - baseY), Ground);
                }

                // Side walls from the floor below to the upper ceiling.
                float top = s.Top.y + WallHeight;
                Vector3 end = s.Top + s.Dir * total;
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector3 inward = -side * sgn;
                    Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 edgeA = s.Top + side * sgn * (ClearWidth * 0.5f), edgeB = end + side * sgn * (ClearWidth * 0.5f);
                    Vector3 start = Vector3.Dot(edgeB - edgeA, dir) > 0 ? edgeA : edgeB;
                    start.y = 0;
                    Wall(g, start, dir, total, baseY, top - baseY, null, seed++, paper: paper);
                }

                // Over the bottom end, from the lower ceiling up to the stair ceiling.
                {
                    Vector3 inward = -s.Dir;
                    Vector3 dir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 a = end - side * (ClearWidth * 0.5f), b = end + side * (ClearWidth * 0.5f);
                    Vector3 start = Vector3.Dot(b - a, dir) > 0 ? a : b;
                    start.y = 0;
                    Wall(g, start, dir, ClearWidth, baseY + WallHeight, h, null, seed++, paper: paper);
                }

                // Ceiling over the whole stairwell, and a lamp halfway down.
                Vector3 mid = (s.Top + end) * 0.5f;
                Rect r = alongX
                    ? Rect.MinMaxRect(Mathf.Min(s.Top.x, end.x), mid.z - ClearWidth * 0.5f, Mathf.Max(s.Top.x, end.x), mid.z + ClearWidth * 0.5f)
                    : Rect.MinMaxRect(mid.x - ClearWidth * 0.5f, Mathf.Min(s.Top.z, end.z), mid.x + ClearWidth * 0.5f, Mathf.Max(s.Top.z, end.z));
                MeshObject("Teto", g, FlatMesh(new[] { r }, top, false, 1f, "TetoEscada"), RoofMaterial(), Scenary);
                CeilingLight(g, new Vector3(mid.x, 0f, mid.z), top, new Color(1f, 0.8f, 0.58f), s.Exit ? 4.8f : 3.6f, 11f, true, Sabotage);
                MarkStatic(g.gameObject);
            }
        }

        // ------------------------------------------------------------------ ducts geometry

        private Material _ductMat, _grateMat, _pipeMat;

        private Material DuctMaterial() => _ductMat != null ? _ductMat : _ductMat = OwnMaterial("M_Duto", new Color(0.42f, 0.44f, 0.46f), 0.85f, 0.38f);
        private Material GrateMaterial() => _grateMat != null ? _grateMat : _grateMat = OwnMaterial("M_Duto_Grade", new Color(0.18f, 0.19f, 0.2f), 0.9f, 0.25f);
        private Material PipeMaterial() => _pipeMat != null ? _pipeMat : _pipeMat = OwnMaterial("M_Duto_Cano", new Color(0.33f, 0.2f, 0.12f), 0.7f, 0.3f);

        private static Material OwnMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = PrototypeAssets.MaterialsFolder + "/" + name + ".mat";
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            UnityEditor.AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private struct Section { public float T0, T1, Floor, Ceil, Width; }

        private void BuildDucts()
        {
            if (Ducts.Count == 0) return;
            var group = Group("Dutos", _root).transform;
            foreach (Duct d in Ducts)
            {
                var g = Group(d.Tunnel ? "TunelManutencao" : "Duto", group).transform;
                Vector3 a = new Vector3(d.A.x, 0, d.A.z), b = new Vector3(d.B.x, 0, d.B.z);
                Vector3 dir = (b - a).normalized;
                float len = Vector3.Distance(a, b);
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                bool alongX = Mathf.Abs(dir.x) > 0.5f;
                Vector3 Size(float along, float height, float across) => alongX ? new Vector3(along, height, across) : new Vector3(across, height, along);
                float baseY = Mathf.Min(d.FloorA, d.FloorB);

                // Sections along the duct: crawl at the mouths, a walkable maintenance tunnel between them
                // when long; a duct between floors steps down in the middle.
                const float tunnelW = 1.8f, tunnelH = 2.3f, crawlEnd = 2.5f;
                var secs = new List<Section>();
                if (Mathf.Abs(d.FloorA - d.FloorB) > 0.5f)
                {
                    int steps = Mathf.CeilToInt(Mathf.Abs(d.FloorA - d.FloorB) / 0.29f);
                    float tread = Mathf.Min(Mathf.Max(len - 2f, 1f) / steps, 0.9f);
                    float startT = (len - tread * steps) * 0.5f;
                    var cuts = new List<float> { 0f };
                    for (int k = 0; k <= steps; k++) cuts.Add(startT + k * tread);
                    cuts.Add(len);
                    for (int k = 0; k < cuts.Count - 1; k++)
                    {
                        if (cuts[k + 1] - cuts[k] < 0.01f) continue;
                        float f = Mathf.Lerp(d.FloorA, d.FloorB, Mathf.Clamp01((float)k / steps));
                        secs.Add(new Section { T0 = cuts[k], T1 = cuts[k + 1], Floor = f, Ceil = f + DuctHeight, Width = DuctWidth });
                    }
                }
                else if (d.Tunnel)
                {
                    secs.Add(new Section { T0 = 0f, T1 = crawlEnd, Floor = d.FloorA, Ceil = d.FloorA + DuctHeight, Width = DuctWidth });
                    secs.Add(new Section { T0 = crawlEnd, T1 = len - crawlEnd, Floor = d.FloorA, Ceil = d.FloorA + tunnelH, Width = tunnelW });
                    secs.Add(new Section { T0 = len - crawlEnd, T1 = len, Floor = d.FloorA, Ceil = d.FloorA + DuctHeight, Width = DuctWidth });
                }
                else secs.Add(new Section { T0 = 0f, T1 = len, Floor = d.FloorA, Ceil = d.FloorA + DuctHeight, Width = DuctWidth });

                // Each section is a closed tube; they overlap by 1 cm so no seam shows.
                foreach (Section s in secs)
                {
                    float l = s.T1 - s.T0 + 0.02f;
                    Vector3 c = a + dir * ((s.T0 + s.T1) * 0.5f);
                    float floorH = Mathf.Max(0.1f, s.Floor - baseY + 0.1f);
                    Box(g, "Piso", c + Vector3.up * (baseY - 0.1f + floorH * 0.5f), Size(l, floorH, s.Width + 0.2f), GrateMaterial(), Ground);
                    Box(g, "Teto", c + Vector3.up * (s.Ceil + 0.05f), Size(l, 0.1f, s.Width + 0.2f), DuctMaterial(), Walls);
                    foreach (float sgn in new[] { -1f, 1f })
                        Box(g, "Lado", c + side * sgn * (s.Width * 0.5f + 0.05f) + Vector3.up * ((s.Floor + s.Ceil) * 0.5f), Size(l, s.Ceil - s.Floor + 0.2f, 0.1f), DuctMaterial(), Walls);
                }

                // Between two sections of different size: a frame that closes the step in the ceiling
                // and the strips beside the narrower one.
                for (int i = 0; i < secs.Count - 1; i++)
                {
                    Section p = secs[i], q = secs[i + 1];
                    float t = p.T1;
                    float wMin = Mathf.Min(p.Width, q.Width), wOut = Mathf.Max(p.Width, q.Width) + 0.2f;
                    float y0 = Mathf.Min(p.Floor, q.Floor) - 0.1f, cMin = Mathf.Min(p.Ceil, q.Ceil), y1 = Mathf.Max(p.Ceil, q.Ceil) + 0.1f;
                    Vector3 c = a + dir * t;
                    if (y1 - cMin > 0.02f)
                        Box(g, "Moldura", c + Vector3.up * ((cMin + y1) * 0.5f), Size(0.1f, y1 - cMin, wOut), DuctMaterial(), Walls);
                    if (wOut - wMin > 0.25f)
                        foreach (float sgn in new[] { -1f, 1f })
                        {
                            float w = (wOut - wMin) * 0.5f;
                            Box(g, "Moldura", c + side * sgn * (wMin * 0.5f + w * 0.5f) + Vector3.up * ((y0 + cMin) * 0.5f), Size(0.1f, cMin - y0, w), DuctMaterial(), Walls);
                        }
                }

                // A duct that starts at a room door: close the rest of the door stub around its mouth.
                foreach (var (end, floor, w) in new[] { (a, d.FloorA, secs[0].Width), (b, d.FloorB, secs[secs.Count - 1].Width) })
                {
                    bool atDoor = _l.Rooms.Any(rw => rw.DoorPoints.Values.Any(p => Vector3.Distance(new Vector3(p.x, 0, p.z), end) < 0.6f));
                    if (!atDoor) continue;
                    Vector3 outward = end == a ? -dir : dir;
                    Vector3 inward = -outward;
                    Vector3 wdir = Vector3.Cross(inward, Vector3.up).normalized;
                    Vector3 start = end - wdir * (ClearWidth * 0.5f);
                    var hole = new List<Opening> { new Opening { From = ClearWidth * 0.5f - w * 0.5f, To = ClearWidth * 0.5f + w * 0.5f, Top = DuctHeight } };
                    Wall(g, start, wdir, ClearWidth, floor, WallHeight, hole, 0);
                }

                // Inside the tunnel: pipes along the top corners and dim red emergency lights every ~12 m.
                foreach (Section s in secs.Where(s => s.Ceil - s.Floor > 2f))
                {
                    float l = s.T1 - s.T0;
                    Vector3 c = a + dir * ((s.T0 + s.T1) * 0.5f);
                    foreach (float sgn in new[] { -1f, 1f })
                        Box(g, "Cano", c + side * sgn * (s.Width * 0.5f - 0.12f) + Vector3.up * (s.Ceil - 0.15f - (sgn > 0 ? 0.18f : 0f)), Size(l, 0.12f, 0.12f), PipeMaterial(), Walls, false);
                    int n = Mathf.Max(1, Mathf.RoundToInt(l / 12f));
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 m = a + dir * (s.T0 + l * (k + 0.5f) / n);
                        var lgo = Place("Assets/Prefabs/Managers/SabotageObjects/SabotageLight.prefab", g, m + Vector3.up * (s.Floor + 2.05f) + side * (s.Width * 0.5f - 0.1f), Quaternion.identity);
                        if (lgo == null) continue;
                        var lt = lgo.GetComponent<Light>();
                        lt.type = LightType.Point; lt.color = new Color(1f, 0.32f, 0.22f); lt.intensity = 1.8f; lt.range = 7f;
                        lt.shadows = LightShadows.None; lt.lightmapBakeType = LightmapBakeType.Mixed;
                        Sabotage.Add(lgo);
                    }
                }
                MarkStatic(g.gameObject);
            }
        }

        private static void Box(Transform g, string name, Vector3 center, Vector3 size, Material mat, int layer, bool collider = true)
        {
            MeshObject(name, g, BoxMesh(size, 1f), mat, layer, center);
            if (collider) BoxColliderAt("Col_" + name, g, center, size, layer);
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

        // SM_Sophisticated_Wall_Double_Door_4m_V1: 4 m along local +z, 0.355 thick on local -x, the
        // opening 1.87 x 2.09 m centred 2.005 m from its start: Door_Double_Black (1.876 x 2.085) fits it unscaled.
        private const float DoorWallCentre = 2.005f, DoorWallThickness = 0.355f, DoorHalf = 0.935f, DoorTop = 2.09f;

        private void PlaceCorridorDoor(Transform parent, Vector3 pos, Vector3 along)
        {
            Vector3 across = Vector3.Cross(Vector3.up, along).normalized;
            // Local z runs across the corridor, local -x (the thickness) towards +along: centre the thickness on pos.
            Vector3 start = pos - across * DoorWallCentre - along * (DoorWallThickness * 0.5f);
            var wall = Place(Hallway + "SM_Sophisticated_Wall_Double_Door_4m_V1.prefab", parent, start, Quaternion.LookRotation(across, Vector3.up));
            if (wall != null)
            {
                SetLayerRecursive(wall, Walls);
                SwapPaper(wall, PaperAt(pos) ?? Mat(Mats + "Hallway/Walls/V2/M_WallHallway_2.mat"));
            }
            float sideW = ClearWidth * 0.5f - DoorHalf;
            foreach (float sgn in new[] { -1f, 1f })
                BoxColliderAt("Col_PortaParede", parent, pos + across * sgn * (DoorHalf + sideW * 0.5f) + Vector3.up * (WallHeight * 0.5f), Size2(along, across, DoorWallThickness, sideW, WallHeight), Walls);
            BoxColliderAt("Col_PortaVerga", parent, pos + Vector3.up * ((DoorTop + WallHeight) * 0.5f), Size2(along, across, DoorWallThickness, DoorHalf * 2f, WallHeight - DoorTop), Walls);
            var door = Place("Assets/Prefabs/Rooms/Props/Doors/Door_Double_Black.prefab", parent, pos, Quaternion.LookRotation(along, Vector3.up));
            if (door != null) Sabotage.Add(door);
        }

        private static Vector3 Size2(Vector3 along, Vector3 across, float alongSize, float acrossSize, float height)
            => Mathf.Abs(along.x) > 0.5f ? new Vector3(alongSize, height, acrossSize) : new Vector3(acrossSize, height, alongSize);

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
                    if (DoorSpots.Any(d => Vector3.Distance(d.pos, pos) < 1.2f)) continue;
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
                    if (DoorSpots.Any(d => Vector3.Distance(d.pos, pos) < 1.5f)) continue;
                    float side = (i + (p.Horizontal ? 0 : 1)) % 2 == 0 ? 1f : -1f;
                    Vector3 across = p.Horizontal ? Vector3.forward : Vector3.right;
                    Vector3 inward = -across * side;
                    Vector3 wall = pos + across * side * (ClearWidth * 0.5f) + Vector3.up * 2.05f;
                    var sconce = Place(Hallway + "SM_Single_Light.prefab", group, wall, FacingX(inward));
                    if (sconce == null) continue;
                    SetLayerRecursive(sconce, PrototypeGeometry.Props);
                    WallFixtures.Add(wall);
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
