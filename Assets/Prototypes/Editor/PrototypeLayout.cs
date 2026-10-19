using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Prototypes.Editor
{
    /// <summary>One piece of corridor, stairs or duct in world space (x/z), between two points on a line.</summary>
    public class WPiece
    {
        public DBar Bar;
        public BarColor Color;
        public bool Horizontal;      // along world x
        public float Line;           // world z for horizontal, world x for vertical
        public float A, B;           // extent along the line, A < B
        public float FloorA, FloorB; // floor height at A and at B (differ for stairs and dropping ducts)
        public Vector3 PA => Horizontal ? new Vector3(A, FloorA, Line) : new Vector3(Line, FloorA, A);
        public Vector3 PB => Horizontal ? new Vector3(B, FloorB, Line) : new Vector3(Line, FloorB, B);
        public float Length => B - A;
        public override string ToString() => $"{Color}{(Horizontal ? "H" : "V")} linha={Line:F1} {A:F1}..{B:F1} ({Length:F1} m) piso {FloorA:F1}->{FloorB:F1}";
    }

    public class WRoom
    {
        public DRoom Room;
        public Vector3 Pivot;
        public float Yaw;
        public Rect Footprint;       // world x/z
        public readonly Dictionary<DDoor, Vector3> DoorPoints = new Dictionary<DDoor, Vector3>(); // where the corridor meets the room
        public float FloorY => Room.Upper ? PrototypeLayout.UpperFloor : 0f;
        public override string ToString() => $"{Room.Kind} pivô=({Pivot.x:F1},{Pivot.y:F1},{Pivot.z:F1}) yaw={Yaw:F0}";
    }

    /// <summary>
    /// Turns a diagram into world positions. What is kept from the drawing is the order of
    /// everything along each axis and which side each door is on; the distances are rebuilt:
    /// rooms take their real size, the hub rooms sit on their real doors, every room lines up with
    /// the corridors entering it, and empty space between things is compressed.
    /// </summary>
    public class PrototypeLayout
    {
        public const float UpperFloor = 4.5f;      // the auditorium's rear platform
        public const float RoomSize = 14.7f;       // puzzle room footprint, door stubs included
        public const float Stub = 8.35f;           // puzzle room pivot to the end of a door stub
        public const float CorridorWidth = 4f;
        public const float GapScale = 0.1215f * 0.7f; // metres per pixel of empty space

        public readonly PrototypeDiagram D;
        public readonly List<WRoom> Rooms = new List<WRoom>();
        public readonly List<WPiece> Pieces = new List<WPiece>();
        public readonly List<string> Problems = new List<string>();
        public readonly List<string> Notes = new List<string>();

        private readonly Dictionary<DBar, int> _line = new Dictionary<DBar, int>();
        private readonly Dictionary<int, float> _lineValue = new Dictionary<int, float>();
        private readonly Dictionary<DBar, bool> _horizontal = new Dictionary<DBar, bool>();
        private float[] _xKeys, _xWorld, _yKeys, _yWorld;

        public WRoom Hub(RoomKind kind) => Rooms.First(r => r.Room.Kind == kind);

        public PrototypeLayout(PrototypeDiagram d)
        {
            D = d;
            DecideOrientation();
            BuildAxisMaps();
            BuildLines();
            PlaceHub();
            PlaceRooms();
            BuildPieces();
            Validate();
        }

        // ---------------------------------------------------------------- orientation

        private bool H(DBar b) => _horizontal[b];

        private void DecideOrientation()
        {
            foreach (DBar b in D.Bars) _horizontal[b] = b.Horizontal;
            foreach (DBar b in D.Bars.Where(b => b.Squarish))
            {
                if (b.Rooms.Count > 0) { _horizontal[b] = b.Rooms[0].side == Side.Left || b.Rooms[0].side == Side.Right; continue; }

                // Between two bars: horizontal when they sit left and right of it.
                var others = b.Touching.Where(o => !o.Squarish).ToList();
                if (others.Count == 0) continue;
                bool leftRight = others.Any(o => o.Px.xMax <= b.Px.xMin + 4 || o.Px.xMin >= b.Px.xMax - 4);
                bool upDown = others.Any(o => o.Px.yMax <= b.Px.yMin + 4 || o.Px.yMin >= b.Px.yMax - 4);
                if (leftRight != upDown) _horizontal[b] = leftRight;
            }
        }

        // ---------------------------------------------------------------- axis maps

        private static float RoomWorldX(DRoom r) => WorldExtent(r).x;
        private static float RoomWorldZ(DRoom r) => WorldExtent(r).y;

        /// <summary>World footprint (x, z) of a room, door stubs included, as it will be rotated.</summary>
        public static Vector2 WorldExtent(DRoom r)
        {
            switch (r.Kind)
            {
                case RoomKind.Auditorium: return new Vector2(32.5f, 41.5f);
                case RoomKind.Bilheteria: return new Vector2(24.7f, 24.7f);
                case RoomKind.Escape: return new Vector2(10.96f, 16.76f);
                case RoomKind.Dressing: return new Vector2(10.03f, 10.16f);
                case RoomKind.Bathroom:
                    bool sideways = r.Doors.Count > 0 && (r.Doors[0].Side == Side.Left || r.Doors[0].Side == Side.Right);
                    return sideways ? new Vector2(12.66f, 6.62f) : new Vector2(6.62f, 12.66f);
                default: return new Vector2(RoomSize, RoomSize);
            }
        }

        private void BuildAxisMaps()
        {
            (_xKeys, _xWorld) = AxisMap(true);
            (_yKeys, _yWorld) = AxisMap(false);
        }

        private (float[], float[]) AxisMap(bool xAxis)
        {
            var keys = new SortedSet<int>();
            foreach (DRoom r in D.Rooms) { keys.Add(xAxis ? r.Px.xMin : r.Px.yMin); keys.Add(xAxis ? r.Px.xMax : r.Px.yMax); }
            foreach (DBar b in D.Bars) { keys.Add(xAxis ? b.Px.xMin : b.Px.yMin); keys.Add(xAxis ? b.Px.xMax : b.Px.yMax); }
            keys.Add(0);
            keys.Add(xAxis ? D.Width : D.Height);
            int[] k = keys.ToArray();
            var world = new float[k.Length];
            for (int i = 1; i < k.Length; i++)
            {
                int a = k[i - 1], b = k[i];
                float scale = GapScale;
                foreach (DRoom r in D.Rooms)
                {
                    int r0 = xAxis ? r.Px.xMin : r.Px.yMin, r1 = xAxis ? r.Px.xMax : r.Px.yMax;
                    if (r0 > a || r1 < b) continue;
                    float s = (xAxis ? RoomWorldX(r) : RoomWorldZ(r)) / (r1 - r0);
                    scale = Mathf.Max(scale, s);
                }
                world[i] = world[i - 1] + (b - a) * scale;
            }
            return (k.Select(v => (float)v).ToArray(), world);
        }

        private static float Map(float[] keys, float[] world, float px)
        {
            int i = System.Array.BinarySearch(keys, px);
            if (i >= 0) return world[i];
            i = ~i;
            if (i <= 0) return world[0];
            if (i >= keys.Length) return world[world.Length - 1];
            float t = (px - keys[i - 1]) / (keys[i] - keys[i - 1]);
            return Mathf.Lerp(world[i - 1], world[i], t);
        }

        private float _x0; // shift so the auditorium sits at the origin
        private readonly List<(float px, float z)> _zAnchors = new List<(float, float)>();
        public float MapX(float px) => Map(_xKeys, _xWorld, px) - _x0;
        private float RawY(float py) => Map(_yKeys, _yWorld, py);

        /// <summary>
        /// North is up in the drawing. Between two hub anchors (real door and wall positions of the
        /// auditorium, Bilheteria and Escape) things keep their relative spacing; outside them the
        /// plain axis map continues.
        /// </summary>
        public float MapZ(float py)
        {
            if (_zAnchors.Count == 0) return -RawY(py);
            var first = _zAnchors[0]; var last = _zAnchors[_zAnchors.Count - 1];
            if (py <= first.px) return first.z + (RawY(first.px) - RawY(py));
            if (py >= last.px) return last.z - (RawY(py) - RawY(last.px));
            for (int i = 1; i < _zAnchors.Count; i++)
            {
                var a = _zAnchors[i - 1]; var b = _zAnchors[i];
                if (py > b.px) continue;
                float ra = RawY(a.px), rb = RawY(b.px);
                float t = rb - ra < 0.001f ? 0f : (RawY(py) - ra) / (rb - ra);
                return Mathf.Lerp(a.z, b.z, t);
            }
            return last.z;
        }

        // ---------------------------------------------------------------- lines

        private int Find(Dictionary<int, int> parent, int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }

        private void BuildLines()
        {
            var parent = D.Bars.ToDictionary(b => b.Id, b => b.Id);
            foreach (DBar b in D.Bars)
            foreach (DBar o in b.Touching)
            {
                if (H(b) != H(o)) continue;
                float cb = H(b) ? b.Px.y + b.Px.height * 0.5f : b.Px.x + b.Px.width * 0.5f;
                float co = H(o) ? o.Px.y + o.Px.height * 0.5f : o.Px.x + o.Px.width * 0.5f;
                if (Mathf.Abs(cb - co) > 8f) continue;
                parent[Find(parent, b.Id)] = Find(parent, o.Id);
            }

            // A straight corridor cut in two by a crossing corridor is still one line.
            foreach (DBar c in D.Bars)
            {
                var across = c.Touching.Where(o => H(o) != H(c)).ToList();
                for (int i = 0; i < across.Count; i++)
                for (int j = i + 1; j < across.Count; j++)
                {
                    DBar p = across[i], q = across[j];
                    float cp = H(p) ? p.Px.y + p.Px.height * 0.5f : p.Px.x + p.Px.width * 0.5f;
                    float cq = H(q) ? q.Px.y + q.Px.height * 0.5f : q.Px.x + q.Px.width * 0.5f;
                    if (Mathf.Abs(cp - cq) > 8f) continue;
                    parent[Find(parent, p.Id)] = Find(parent, q.Id);
                }
            }
            foreach (DBar b in D.Bars) _line[b] = Find(parent, b.Id);
        }

        private float LineCenterPx(int line)
        {
            var bars = D.Bars.Where(b => _line[b] == line).ToList();
            return bars.Average(b => H(b) ? b.Px.y + b.Px.height * 0.5f : b.Px.x + b.Px.width * 0.5f);
        }

        private bool LineHorizontal(int line) => H(D.Bars.First(b => _line[b] == line));

        private void Force(DBar bar, float value, string why)
        {
            int line = _line[bar];
            if (_lineValue.TryGetValue(line, out float old) && Mathf.Abs(old - value) > 0.01f)
            {
                if (Mathf.Abs(old - value) <= 1.5f) { _lineValue[line] = (old + value) * 0.5f; Notes.Add($"linha de {bar} entre {old:F2} e {value:F2} ({why}): usei a média"); return; }
                Problems.Add($"{bar}: duas posições pedidas ({old:F2} e {value:F2}, {why})");
                return;
            }
            _lineValue[line] = value;
        }

        public float LineValue(DBar bar)
        {
            int line = _line[bar];
            if (_lineValue.TryGetValue(line, out float v)) return v;
            float c = LineCenterPx(line);
            v = LineHorizontal(line) ? MapZ(c) : MapX(c);
            _lineValue[line] = v;
            return v;
        }

        // ---------------------------------------------------------------- hub

        private WRoom Add(DRoom r, Vector3 pivot, float yaw)
        {
            var w = new WRoom { Room = r, Pivot = pivot, Yaw = yaw };
            Rooms.Add(w);
            return w;
        }

        private void PlaceHub()
        {
            DRoom aud = D.Rooms.First(r => r.Kind == RoomKind.Auditorium);
            DRoom bil = D.Rooms.First(r => r.Kind == RoomKind.Bilheteria);
            DRoom esc = D.Rooms.First(r => r.Kind == RoomKind.Escape);

            // Auditorium at the origin, stage to the south. Its side doors fix the corridors on both sides.
            float audCx = Map(_xKeys, _xWorld, aud.CenterPx.x);
            _x0 = audCx;
            var sideBars = aud.Doors.Where(d => d.Side == Side.Left || d.Side == Side.Right).Select(d => d.Bar).ToList();
            float sidePx = sideBars.Count > 0 ? sideBars.Average(b => b.Px.y + b.Px.height * 0.5f) : aud.CenterPx.y;

            float gap = 6f;
            if (D.Bars.Any(b => b.Color != BarColor.Red && H(b) && b.Px.yMin > bil.Px.yMax && b.Px.yMax < aud.Px.yMin)) gap = 12f; // a balcony branch crosses the links
            float bz = 25.38f + gap + 12.36f;
            float ez = bz + 12.36f + 5f + 4.40f; // the Escape entrance (its single door stub) 5 m north of the Bilheteria

            // North-south anchors: real walls and doors of the hub rooms.
            _zAnchors.Clear();
            _zAnchors.Add((esc.Px.yMin, ez + 12.36f));
            _zAnchors.Add((esc.Px.yMax, ez - 4.40f));
            _zAnchors.Add((bil.Px.yMin, bz + 12.36f));
            _zAnchors.Add((bil.Px.yMax, bz - 12.36f));
            _zAnchors.Add((aud.Px.yMin, 25.38f));
            _zAnchors.Add((sidePx, -1.85f));
            _zAnchors.Add((aud.Px.yMax, -16.13f));
            _zAnchors.Sort((p, q) => p.px.CompareTo(q.px));

            var a = Add(aud, Vector3.zero, 0f);
            a.Footprint = new Rect(-16.23f, -16.13f, 32.46f, 41.51f);

            foreach (DDoor d in aud.Doors)
            {
                switch (d.Side)
                {
                    case Side.Left: a.DoorPoints[d] = new Vector3(-16.23f, 0f, -1.85f); Force(d.Bar, -1.85f, "porta lateral do auditório"); break;
                    case Side.Right: a.DoorPoints[d] = new Vector3(16.23f, 0f, -1.85f); Force(d.Bar, -1.85f, "porta lateral do auditório"); break;
                    case Side.Bottom: a.DoorPoints[d] = new Vector3(0f, 0f, -16.13f); Force(d.Bar, 0f, "porta dos bastidores"); break;
                    case Side.Top:
                        float x = d.Bar.Px.center.x < aud.CenterPx.x ? -6.95f : 6.95f;
                        a.DoorPoints[d] = new Vector3(x, UpperFloor, 25.38f);
                        Force(d.Bar, x, "porta do fundo do auditório");
                        break;
                }
            }

            // Bilheteria north of the auditorium, on the upper floor, turned so its two double doors
            // face the auditorium and the centre double door faces the Escape.
            var b0 = Add(bil, new Vector3(0f, UpperFloor, bz), 90f);
            b0.Footprint = new Rect(-12.36f, bz - 12.36f, 24.72f, 24.72f);
            foreach (DDoor d in bil.Doors)
            {
                switch (d.Side)
                {
                    case Side.Top: b0.DoorPoints[d] = new Vector3(0f, UpperFloor, bz + 12.36f); Force(d.Bar, 0f, "porta da bilheteria p/ escape"); break;
                    case Side.Left: b0.DoorPoints[d] = new Vector3(-12.36f, UpperFloor, bz + 2f); Force(d.Bar, bz + 2f, "porta oeste da bilheteria"); break;
                    case Side.Right: b0.DoorPoints[d] = new Vector3(12.36f, UpperFloor, bz + 2f); Force(d.Bar, bz + 2f, "porta leste da bilheteria"); break;
                    case Side.Bottom:
                        float x = d.Bar.Px.center.x < bil.CenterPx.x ? -6.95f : 6.95f; // in line with the auditorium rear doors
                        b0.DoorPoints[d] = new Vector3(x, UpperFloor, bz - 12.36f);
                        Force(d.Bar, x, "porta da bilheteria p/ auditório");
                        break;
                }
            }

            // Escape north of the Bilheteria, turned so you come in through its main (single) door and the
            // escape exit (the double door of EscapeDoorManager) is the far wall, facing away from the level.
            var e = Add(esc, new Vector3(1.0f, UpperFloor, ez), 270f); // its door stub is centred 1 m off the pivot
            e.Footprint = new Rect(1.0f - 6.48f, ez - 4.40f, 10.96f, 16.76f);
            e.DoorPoints[esc.Doors[0]] = new Vector3(0f, UpperFloor, ez - 4.40f);
        }

        // ---------------------------------------------------------------- rooms

        private static readonly Dictionary<(Side, Side), float> PuzzleYaw = new Dictionary<(Side, Side), float>
        {
            { (Side.Left, Side.Bottom), 0f }, { (Side.Top, Side.Left), 90f }, { (Side.Right, Side.Top), 180f }, { (Side.Bottom, Side.Right), 270f },
        };

        private static Vector3 Rot(float yaw, Vector3 v) => Quaternion.Euler(0f, yaw, 0f) * v;

        private static Rect RotRect(Vector3 pivot, float yaw, float minX, float minZ, float maxX, float maxZ)
        {
            var pts = new[] { new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, maxZ) }.Select(p => pivot + Rot(yaw, p)).ToList();
            float x0 = pts.Min(p => p.x), x1 = pts.Max(p => p.x), z0 = pts.Min(p => p.z), z1 = pts.Max(p => p.z);
            return new Rect(x0, z0, x1 - x0, z1 - z0);
        }

        private void PlaceRooms()
        {
            foreach (DRoom r in D.Rooms)
            {
                if (r.Kind == RoomKind.Auditorium || r.Kind == RoomKind.Bilheteria || r.Kind == RoomKind.Escape) continue;
                float y = r.Upper ? UpperFloor : 0f;

                if (r.Kind == RoomKind.Puzzle)
                {
                    DDoor hDoor = r.Doors.First(d => d.Side == Side.Left || d.Side == Side.Right);
                    DDoor vDoor = r.Doors.First(d => d.Side == Side.Top || d.Side == Side.Bottom);
                    float x = LineValue(vDoor.Bar), z = LineValue(hDoor.Bar);
                    var key = PuzzleYaw.Keys.First(k => (k.Item1 == hDoor.Side || k.Item1 == vDoor.Side) && (k.Item2 == hDoor.Side || k.Item2 == vDoor.Side));
                    float yaw = PuzzleYaw[key];
                    var w = Add(r, new Vector3(x, y, z), yaw);
                    w.Footprint = RotRect(w.Pivot, yaw, -Stub, -Stub, 6.35f, 6.35f);
                    foreach (DDoor d in r.Doors) w.DoorPoints[d] = w.Pivot + Rot(yaw, d == DoorOnLocalWest(r, yaw) ? new Vector3(-Stub, 0, 0) : new Vector3(0, 0, -Stub));
                    continue;
                }

                DDoor door = r.Doors[0];
                bool sideways = door.Side == Side.Left || door.Side == Side.Right;
                float line = LineValue(door.Bar);
                float other = sideways ? MapX(r.CenterPx.x) : MapZ(r.CenterPx.y);

                if (r.Kind == RoomKind.Bathroom)
                {
                    // Door on the local -x short side, opening centred at local z -0.1, stub end at x -10.31.
                    float yaw = door.Side == Side.Left ? 0f : door.Side == Side.Right ? 180f : door.Side == Side.Top ? 90f : 270f;
                    Vector3 doorLocal = new Vector3(-10.31f, 0f, -0.1f);
                    // Put the room so its door lands on the corridor line, its body on the far side of the door.
                    Vector3 pivot = sideways ? new Vector3(other, y, line) : new Vector3(line, y, other);
                    Vector3 doorWorld = pivot + Rot(yaw, doorLocal);
                    Vector3 fix = sideways ? new Vector3(0, 0, line - doorWorld.z) : new Vector3(line - doorWorld.x, 0, 0);
                    // and so the body sits where the box was drawn: centre of the body on the drawn centre.
                    Vector3 body = pivot + fix + Rot(yaw, new Vector3(-3.98f, 0f, -0.09f));
                    Vector3 slide = sideways ? new Vector3(other - body.x, 0, 0) : new Vector3(0, 0, other - body.z);
                    pivot += fix + slide;
                    var w = Add(r, pivot, yaw);
                    w.Footprint = RotRect(pivot, yaw, -10.31f, -3.40f, 2.35f, 3.22f);
                    w.DoorPoints[door] = pivot + Rot(yaw, doorLocal);
                    continue;
                }

                if (r.Kind == RoomKind.Dressing)
                {
                    // DressingRoom _Game: door at local (10.0, -7.45), stub end at z -9.65; turned 180° when the door faces north.
                    float yaw = door.Side == Side.Top ? 180f : door.Side == Side.Bottom ? 0f : door.Side == Side.Left ? 270f : 90f;
                    Vector3 doorLocal = new Vector3(10.0f, 0f, -9.65f);
                    Vector3 centreLocal = new Vector3(9.99f, 0f, -4.57f);
                    Vector3 pivot = Vector3.zero;
                    Vector3 doorWorld = Rot(yaw, doorLocal), centreWorld = Rot(yaw, centreLocal);
                    if (sideways) pivot = new Vector3(other - centreWorld.x, y, line - doorWorld.z);
                    else pivot = new Vector3(line - doorWorld.x, y, other - centreWorld.z);
                    var w = Add(r, pivot, yaw);
                    w.Footprint = RotRect(pivot, yaw, 4.97f, -9.65f, 15f, 0.51f);
                    w.DoorPoints[door] = pivot + doorWorld;
                    continue;
                }

                Problems.Add($"{r}: tipo sem regra de posição");
            }

            // The Dressing must sit south of the stage: if the drawing put it closer, push it out.
            var dressing = Rooms.FirstOrDefault(w => w.Room.Kind == RoomKind.Dressing);
            if (dressing != null && dressing.Footprint.yMax > -16.13f - 6f)
            {
                float push = dressing.Footprint.yMax - (-16.13f - 6f);
                dressing.Pivot.z -= push;
                dressing.Footprint.y -= push;
                foreach (var k in dressing.DoorPoints.Keys.ToList()) dressing.DoorPoints[k] -= new Vector3(0, 0, push);
                Notes.Add($"camarim empurrado {push:F1} m para o sul do palco");
            }
        }

        /// <summary>Which of a puzzle room's two doors ends up on its local west (-x) wall for the given yaw.</summary>
        private static DDoor DoorOnLocalWest(DRoom r, float yaw)
        {
            Vector3 west = Rot(yaw, Vector3.left);
            Side s = Mathf.Abs(west.x) > 0.5f ? (west.x < 0 ? Side.Left : Side.Right) : (west.z > 0 ? Side.Top : Side.Bottom);
            return r.Doors.First(d => d.Side == s);
        }

        // ---------------------------------------------------------------- pieces

        private float EndAt(DBar bar, bool atStart)
        {
            bool h = H(bar);
            int px = atStart ? (h ? bar.Px.xMin : bar.Px.yMin) : (h ? bar.Px.xMax : bar.Px.yMax);

            // A room door at this end: the corridor runs to the end of the door stub.
            foreach (var (room, side) in bar.Rooms)
            {
                bool thisEnd = h ? (atStart ? side == Side.Right : side == Side.Left) : (atStart ? side == Side.Bottom : side == Side.Top);
                if (!thisEnd) continue;
                var w = Rooms.FirstOrDefault(x => x.Room == room);
                var door = room.Doors.First(dd => dd.Bar == bar);
                if (w != null && w.DoorPoints.TryGetValue(door, out var p)) return h ? p.x : p.z;
            }

            // Another bar across this end: meet its centre line.
            foreach (DBar o in bar.Touching)
            {
                if (H(o) == h) continue;
                RectInt b = bar.Px, q = o.Px;
                bool atThisEnd = h ? (atStart ? Mathf.Abs(q.xMax - b.xMin) <= 6 || (q.xMin <= b.xMin && q.xMax >= b.xMin) : Mathf.Abs(q.xMin - b.xMax) <= 6 || (q.xMin <= b.xMax && q.xMax >= b.xMax))
                                   : (atStart ? Mathf.Abs(q.yMax - b.yMin) <= 6 || (q.yMin <= b.yMin && q.yMax >= b.yMin) : Mathf.Abs(q.yMin - b.yMax) <= 6 || (q.yMin <= b.yMax && q.yMax >= b.yMax));
                if (atThisEnd) return LineValue(o);
            }

            // Collinear continuation or a free end: where it was drawn.
            return h ? MapX(px) : MapZ(px);
        }

        private float FloorOf(DBar bar) => bar.Color == BarColor.Green ? UpperFloor : 0f;

        private void BuildPieces()
        {
            foreach (DBar bar in D.Bars)
            {
                bool h = H(bar);
                float line = LineValue(bar);
                float e0 = EndAt(bar, true), e1 = EndAt(bar, false);
                var p = new WPiece { Bar = bar, Color = bar.Color, Horizontal = h, Line = line, A = Mathf.Min(e0, e1), B = Mathf.Max(e0, e1) };
                p.FloorA = p.FloorB = FloorOf(bar);
                if (bar.Color == BarColor.Yellow) DuctFloors(p);
                Pieces.Add(p);
            }
        }

        /// <summary>A duct is flat unless one end reaches the upper floor (a green bar or a blue room).</summary>
        private void DuctFloors(WPiece p)
        {
            float FloorAtEnd(bool atA)
            {
                Vector3 end = atA ? p.PA : p.PB;
                foreach (var (room, _) in p.Bar.Rooms)
                {
                    var w = Rooms.FirstOrDefault(x => x.Room == room);
                    if (w == null) continue;
                    var door = room.Doors.First(dd => dd.Bar == p.Bar);
                    if (w.DoorPoints.TryGetValue(door, out var dp) && Vector3.Distance(new Vector3(dp.x, 0, dp.z), new Vector3(end.x, 0, end.z)) < 3f) return w.FloorY;
                }
                foreach (DBar o in p.Bar.Touching)
                {
                    if (o.Color == BarColor.Yellow) continue;
                    var op = Pieces.FirstOrDefault(x => x.Bar == o);
                    float oline = LineValue(o);
                    bool near = p.Horizontal == H(o) ? false : Mathf.Abs((atA ? p.A : p.B) - oline) < 3f;
                    if (near || (p.Horizontal == H(o))) return o.Color == BarColor.Green ? UpperFloor : 0f;
                }
                return 0f;
            }
            p.FloorA = FloorAtEnd(true);
            p.FloorB = FloorAtEnd(false);
        }

        // ---------------------------------------------------------------- validation

        private void Validate()
        {
            // Rooms must not overlap.
            for (int i = 0; i < Rooms.Count; i++)
            for (int j = i + 1; j < Rooms.Count; j++)
            {
                if (Mathf.Abs(Rooms[i].FloorY - Rooms[j].FloorY) > 1f && Rooms[i].Room.Kind != RoomKind.Auditorium && Rooms[j].Room.Kind != RoomKind.Auditorium) continue;
                Rect a = Rooms[i].Footprint, b = Rooms[j].Footprint;
                if (Shrink(a, 0.3f).Overlaps(Shrink(b, 0.3f))) Problems.Add($"salas sobrepostas: {Rooms[i]} × {Rooms[j]}");
            }

            // Corridors must not run through rooms (except where they enter).
            foreach (WPiece p in Pieces)
            {
                Rect pr = PieceRect(p, 0.2f);
                foreach (WRoom r in Rooms)
                {
                    if (p.Bar.Rooms.Any(x => x.room == r.Room)) continue;
                    if (Mathf.Abs(r.FloorY - Mathf.Min(p.FloorA, p.FloorB)) > 1f && Mathf.Abs(r.FloorY - Mathf.Max(p.FloorA, p.FloorB)) > 1f && r.Room.Kind != RoomKind.Auditorium) continue;
                    if (pr.Overlaps(Shrink(r.Footprint, 0.3f))) Problems.Add($"corredor atravessa sala: {p} × {r}");
                }
                if (p.Length < 0.5f && p.Color != BarColor.Yellow) Notes.Add($"trecho muito curto: {p}");
            }

            // Corridors on the same floor that are not connected must keep a wall's distance.
            for (int i = 0; i < Pieces.Count; i++)
            for (int j = i + 1; j < Pieces.Count; j++)
            {
                WPiece a = Pieces[i], b = Pieces[j];
                if (a.Bar.Touching.Contains(b.Bar)) continue;
                if (Mathf.Abs(a.FloorA - b.FloorA) > 1f) continue;

                // Two pieces of one straight corridor meeting at a crossing.
                if (a.Horizontal == b.Horizontal && Mathf.Abs(a.Line - b.Line) < 0.6f) continue;

                Rect ra = PieceRect(a, 0.4f), rb = PieceRect(b, 0.4f);
                if (!ra.Overlaps(rb)) continue;

                // Both end on the same corridor and only meet inside it.
                Rect overlap = Rect.MinMaxRect(Mathf.Max(ra.xMin, rb.xMin), Mathf.Max(ra.yMin, rb.yMin), Mathf.Min(ra.xMax, rb.xMax), Mathf.Min(ra.yMax, rb.yMax));
                bool insideCommon = Pieces.Any(c => c != a && c != b && a.Bar.Touching.Contains(c.Bar) && b.Bar.Touching.Contains(c.Bar)
                                                    && PieceRect(c, 0.5f).Contains(overlap.min) && PieceRect(c, 0.5f).Contains(overlap.max));
                if (insideCommon) continue;

                Problems.Add($"corredores colados sem ligação: {a} × {b}");
            }
        }

        public static Rect PieceRect(WPiece p, float grow)
        {
            float half = (p.Color == BarColor.Yellow ? 1.0f : CorridorWidth * 0.5f) + grow;
            return p.Horizontal ? new Rect(p.A - grow, p.Line - half, p.Length + grow * 2, half * 2) : new Rect(p.Line - half, p.A - grow, half * 2, p.Length + grow * 2);
        }

        private static Rect Shrink(Rect r, float by) => new Rect(r.x + by, r.y + by, r.width - by * 2, r.height - by * 2);

        public string Report()
        {
            var sb = new StringBuilder();
            float x0 = Rooms.Min(r => r.Footprint.xMin), x1 = Rooms.Max(r => r.Footprint.xMax), z0 = Rooms.Min(r => r.Footprint.yMin), z1 = Rooms.Max(r => r.Footprint.yMax);
            sb.AppendLine($"área: {x1 - x0:F0} × {z1 - z0:F0} m");
            var lengths = Pieces.Where(p => p.Color == BarColor.Red || p.Color == BarColor.Green).Select(p => p.Length).ToList();
            sb.AppendLine($"trechos: {Pieces.Count}; maior {lengths.Max():F1} m; acima de 25 m: {lengths.Count(l => l > 25f)}");
            foreach (string n in Notes) sb.AppendLine("  nota: " + n);
            foreach (string p in Problems) sb.AppendLine("  PROBLEMA: " + p);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- debug image

        public void SavePreview(string path)
        {
            float x0 = Mathf.Min(Rooms.Min(r => r.Footprint.xMin), Pieces.Min(p => PieceRect(p, 0).xMin)) - 5;
            float x1 = Mathf.Max(Rooms.Max(r => r.Footprint.xMax), Pieces.Max(p => PieceRect(p, 0).xMax)) + 5;
            float z0 = Mathf.Min(Rooms.Min(r => r.Footprint.yMin), Pieces.Min(p => PieceRect(p, 0).yMin)) - 5;
            float z1 = Mathf.Max(Rooms.Max(r => r.Footprint.yMax), Pieces.Max(p => PieceRect(p, 0).yMax)) + 5;
            const float ppm = 6f;
            int w = Mathf.CeilToInt((x1 - x0) * ppm), h = Mathf.CeilToInt((z1 - z0) * ppm);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var fill = new Color32[w * h];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(179, 179, 179, 255);
            tex.SetPixels32(fill);

            void Box(Rect r, Color c)
            {
                int ax = Mathf.Clamp(Mathf.FloorToInt((r.xMin - x0) * ppm), 0, w - 1), bx = Mathf.Clamp(Mathf.CeilToInt((r.xMax - x0) * ppm), 0, w - 1);
                int az = Mathf.Clamp(Mathf.FloorToInt((r.yMin - z0) * ppm), 0, h - 1), bz = Mathf.Clamp(Mathf.CeilToInt((r.yMax - z0) * ppm), 0, h - 1);
                for (int y = az; y <= bz; y++) for (int x = ax; x <= bx; x++) tex.SetPixel(x, y, c);
            }

            foreach (WPiece p in Pieces)
            {
                Color c = p.Color == BarColor.Red ? new Color(1f, 0.2f, 0.2f) : p.Color == BarColor.Green ? new Color(0f, 0.75f, 0.4f) : new Color(1f, 0.85f, 0.35f);
                Box(PieceRect(p, 0), c);
            }
            foreach (WRoom r in Rooms)
            {
                Color c = r.Room.Upper ? new Color(0f, 0.59f, 0.71f) : Color.black;
                if (r.Room.Kind == RoomKind.Auditorium) c = new Color(0.25f, 0.1f, 0.1f);
                Box(r.Footprint, c);
                foreach (var dp in r.DoorPoints.Values) Box(new Rect(dp.x - 0.8f, dp.z - 0.8f, 1.6f, 1.6f), Color.white);
            }
            // 25 m scale bar
            Box(new Rect(x0 + 3, z0 + 2, 25, 0.8f), Color.white);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
