using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Prototypes.Editor
{
    public enum BarColor { Red, Green, Yellow }

    public enum Side { Left, Right, Top, Bottom }

    public enum RoomKind { Puzzle, Bathroom, Escape, Bilheteria, Auditorium, Dressing, Unknown }

    /// <summary>A rectangle of corridor colour as drawn: a straight piece of corridor, stairs or duct.</summary>
    public class DBar
    {
        public int Id;
        public BarColor Color;
        public RectInt Px;              // x, y (image space, y down), width, height
        public bool Horizontal => Px.width >= Px.height;

        // A short stub drawn about as wide as it is long: its direction comes from what it connects.
        public bool Squarish => Mathf.Abs(Px.width - Px.height) <= 10 || Mathf.Max(Px.width, Px.height) < 40;
        public float Center => Horizontal ? Px.y + Px.height * 0.5f : Px.x + Px.width * 0.5f;
        public int A0 => Horizontal ? Px.xMin : Px.yMin;
        public int A1 => Horizontal ? Px.xMax : Px.yMax;
        public readonly List<DBar> Touching = new List<DBar>();
        public readonly List<(DRoom room, Side side)> Rooms = new List<(DRoom, Side)>();
        public override string ToString() => $"{Color}#{Id}{(Horizontal ? "H" : "V")}[{Px.xMin}..{Px.xMax - 1},{Px.yMin}..{Px.yMax - 1}]";
    }

    public class DDoor
    {
        public Side Side;
        public DBar Bar;
    }

    public class DRoom
    {
        public int Id;
        public RectInt Px;
        public bool Blue;
        public RoomKind Kind = RoomKind.Unknown;
        public bool Upper => Blue;
        public readonly List<DDoor> Doors = new List<DDoor>();
        public Vector2 CenterPx => new Vector2(Px.x + Px.width * 0.5f, Px.y + Px.height * 0.5f);
        public override string ToString() => $"{Kind}#{Id}({(Blue ? "cima" : "térreo")})[{Px.xMin}..{Px.xMax - 1},{Px.yMin}..{Px.yMax - 1}]";
    }

    /// <summary>
    /// Reads a level drawn on the canvas (legend top-left): black/blue boxes are rooms on the ground
    /// and upper floor, red/green bars corridors on the ground and upper floor, yellow bars crouch ducts.
    /// Which box is which room comes from where it is and how it connects, not from its label.
    /// </summary>
    public class PrototypeDiagram
    {
        public readonly List<DRoom> Rooms = new List<DRoom>();
        public readonly List<DBar> Bars = new List<DBar>();
        public readonly List<string> Problems = new List<string>();
        public int Width, Height;

        private const int Touch = 4;

        public static PrototypeDiagram Load(string pngPath)
        {
            var d = new PrototypeDiagram();
            d.Parse(File.ReadAllBytes(pngPath));
            d.Connect();
            d.Classify();
            return d;
        }

        private void Parse(byte[] png)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(png);
            Width = tex.width;
            Height = tex.height;
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);

            // 0 none, 1 black, 2 blue, 3 red, 4 green, 5 yellow, 6 white
            var cls = new byte[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Color32 c = px[(Height - 1 - y) * Width + x];
                byte k = 0;
                if (x < 350 && y < 345) k = 0; // legend
                else if (c.r < 45 && c.g < 45 && c.b < 45) k = 1;
                else if (c.r < 50 && c.g > 120 && c.g < 180 && c.b > 150) k = 2;
                else if (c.r > 200 && c.g < 100 && c.b < 100) k = 3;
                else if (c.r < 70 && c.g > 150 && c.b < 150) k = 4;
                else if (c.r > 220 && c.g > 180 && c.b < 140) k = 5;
                else if (c.r > 215 && c.g > 215 && c.b > 215) k = 6;
                cls[y * Width + x] = k;
            }

            // Rooms: connected black/blue, white label text included.
            var seen = new bool[Width * Height];
            var queue = new Queue<int>();
            for (int i = 0; i < cls.Length; i++)
            {
                if (seen[i] || (cls[i] != 1 && cls[i] != 2)) continue;
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, blue = 0, black = 0, n = 0;
                queue.Enqueue(i);
                seen[i] = true;
                while (queue.Count > 0)
                {
                    int p = queue.Dequeue();
                    int x = p % Width, y = p / Width;
                    n++;
                    if (cls[p] == 2) blue++;
                    if (cls[p] == 1) black++;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    foreach (int np in new[] { p - 1, p + 1, p - Width, p + Width })
                    {
                        if (np < 0 || np >= cls.Length || seen[np]) continue;
                        byte k = cls[np];
                        if (k == 1 || k == 2 || k == 6) { seen[np] = true; queue.Enqueue(np); }
                    }
                }
                if (n < 3000) continue;
                Rooms.Add(new DRoom { Id = Rooms.Count, Px = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1), Blue = blue > black });
            }

            // Bars: per colour, runs per row merged downwards into rectangles.
            foreach (var (k, color) in new[] { ((byte)3, BarColor.Red), ((byte)4, BarColor.Green), ((byte)5, BarColor.Yellow) })
            {
                var open = new List<int[]>();
                var done = new List<int[]>();
                for (int y = 0; y < Height; y++)
                {
                    var runs = new List<int[]>();
                    int x = 0;
                    while (x < Width)
                    {
                        if (cls[y * Width + x] != k) { x++; continue; }
                        int s = x;
                        while (x < Width && cls[y * Width + x] == k) x++;
                        if (x - s >= 6) runs.Add(new[] { s, x - 1 });
                    }
                    var next = new List<int[]>();
                    foreach (var r in runs)
                    {
                        var m = open.Find(o => Mathf.Abs(o[0] - r[0]) <= 2 && Mathf.Abs(o[1] - r[1]) <= 2);
                        if (m != null) { open.Remove(m); m[3] = y; next.Add(m); }
                        else next.Add(new[] { r[0], r[1], y, y });
                    }
                    done.AddRange(open);
                    open = next;
                }
                done.AddRange(open);
                foreach (var r in done)
                {
                    int w = r[1] - r[0] + 1, h = r[3] - r[2] + 1;
                    if (w < 12 || h < 12) continue;
                    Bars.Add(new DBar { Id = Bars.Count, Color = color, Px = new RectInt(r[0], r[2], w, h) });
                }
            }
        }

        private static bool Near(RectInt a, RectInt b, int tol)
        {
            return a.xMin <= b.xMax + tol && b.xMin <= a.xMax + tol && a.yMin <= b.yMax + tol && b.yMin <= a.yMax + tol;
        }

        private void Connect()
        {
            for (int i = 0; i < Bars.Count; i++)
            for (int j = i + 1; j < Bars.Count; j++)
            {
                if (!Near(Bars[i].Px, Bars[j].Px, Touch)) continue;
                Bars[i].Touching.Add(Bars[j]);
                Bars[j].Touching.Add(Bars[i]);
            }

            foreach (DRoom room in Rooms)
            foreach (DBar bar in Bars)
            {
                if (!Near(room.Px, bar.Px, Touch)) continue;
                RectInt r = room.Px, b = bar.Px;
                Side side;
                // Which edge of the room the bar comes out of: the one the bar overlaps least past.
                float dl = Mathf.Abs(b.xMax - r.xMin), dr = Mathf.Abs(b.xMin - r.xMax), dt = Mathf.Abs(b.yMax - r.yMin), db = Mathf.Abs(b.yMin - r.yMax);
                float min = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(dt, db));
                if (min == dl) side = Side.Left;
                else if (min == dr) side = Side.Right;
                else if (min == dt) side = Side.Top;
                else side = Side.Bottom;

                // A bar running along a room edge without entering it is not a door.
                bool across = bar.Squarish || (side == Side.Left || side == Side.Right ? bar.Horizontal : !bar.Horizontal);
                if (!across) continue;

                room.Doors.Add(new DDoor { Side = side, Bar = bar });
                bar.Rooms.Add((room, side));
            }
        }

        private void Classify()
        {
            // The hub column: the Escape is the top-most blue box with one door; the Bilheteria the
            // blue box under it; the Auditorium the box reached from the Bilheteria by two green bars.
            DRoom escape = Rooms.Where(r => r.Blue && r.Doors.Count == 1).OrderBy(r => r.Px.yMin)
                .FirstOrDefault(r => r.Doors[0].Side == Side.Bottom && r.Doors[0].Bar.Color == BarColor.Green
                                     && Rooms.Any(o => o != r && o.Blue && o.Doors.Any(d => d.Bar == r.Doors[0].Bar)));
            if (escape == null) { Problems.Add("não achei a Escape Room"); return; }
            escape.Kind = RoomKind.Escape;

            DRoom bilheteria = Rooms.First(o => o != escape && o.Doors.Any(d => d.Bar == escape.Doors[0].Bar));
            bilheteria.Kind = RoomKind.Bilheteria;

            var bilBottom = bilheteria.Doors.Where(d => d.Side == Side.Bottom).Select(d => d.Bar).ToList();
            DRoom auditorium = Rooms.FirstOrDefault(o => !o.Blue && o.Doors.Count(d => d.Side == Side.Top && bilBottom.Contains(d.Bar)) >= 2);
            if (auditorium == null)
            {
                // The links may cross a junction (a balcony branch) before reaching the auditorium.
                auditorium = Rooms.Where(o => !o.Blue && o.Doors.Count(d => d.Side == Side.Top && d.Bar.Color == BarColor.Green) >= 2)
                    .OrderBy(o => Mathf.Abs(o.CenterPx.x - bilheteria.CenterPx.x)).FirstOrDefault();
            }
            if (auditorium == null) { Problems.Add("não achei o Auditorium"); return; }
            auditorium.Kind = RoomKind.Auditorium;

            foreach (DRoom r in Rooms)
            {
                if (r == escape || r == bilheteria || r == auditorium) continue;
                if (r.Doors.Count == 2) { r.Kind = RoomKind.Puzzle; continue; }
                if (r.Doors.Count == 1)
                {
                    bool underAuditorium = !r.Blue && Mathf.Abs(r.CenterPx.x - auditorium.CenterPx.x) < 40 && r.Px.yMin > auditorium.Px.yMax;
                    r.Kind = underAuditorium ? RoomKind.Dressing : RoomKind.Bathroom;
                    continue;
                }
                Problems.Add($"{r}: {r.Doors.Count} portas (sala precisa de 2, banheiro/camarim de 1)");
            }

            foreach (DRoom r in Rooms.Where(r => r.Kind == RoomKind.Puzzle))
            {
                Side a = r.Doors[0].Side, b = r.Doors[1].Side;
                bool opposite = (a == Side.Left && b == Side.Right) || (a == Side.Right && b == Side.Left) || (a == Side.Top && b == Side.Bottom) || (a == Side.Bottom && b == Side.Top);
                if (opposite || a == b) Problems.Add($"{r}: portas {a}+{b} não são frente + lado");
            }

            if (Rooms.Count(r => r.Kind == RoomKind.Dressing) != 1) Problems.Add("camarim: esperava 1, achei " + Rooms.Count(r => r.Kind == RoomKind.Dressing));
        }

        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Rooms.Count} salas, {Bars.Count} trechos");
            foreach (var kind in System.Enum.GetValues(typeof(RoomKind)).Cast<RoomKind>())
            {
                var list = Rooms.Where(r => r.Kind == kind).ToList();
                if (list.Count == 0) continue;
                sb.AppendLine($"  {kind}: {list.Count} ({list.Count(r => r.Blue)} em cima)");
            }
            foreach (DRoom r in Rooms) sb.AppendLine("  " + r + " portas: " + string.Join(", ", r.Doors.Select(d => d.Side + "→" + d.Bar)));
            foreach (string p in Problems) sb.AppendLine("  PROBLEMA: " + p);
            return sb.ToString();
        }
    }
}
