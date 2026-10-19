using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// Dresses the corridors as a theatre abandoned mid-season, following the level design notes:
    /// every prop answers "why is it here for gameplay" and "why is it here for the world".
    /// - The ground floor is the service side (boxes, stage gear, costumes, lockers, notice boards);
    ///   the upper floor is the public side (portraits, posters, benches, popcorn left behind).
    /// - Props stand against the walls, one side at a time, leaving at least 2.6 m to run through;
    ///   the bigger ones break the long runs and give cover in a chase.
    /// - Exit signs point up the stairs towards the Bilheteria and the Escape (direction).
    /// - Objects worth filming sit where it is risky to stop (desire: risk for reward).
    /// - Damp, stains and litter everywhere: nobody has been here for a long time (show, don't tell).
    /// </summary>
    public static class PrototypeDressing
    {
        private const string HW = "Assets/Prefabs/Rooms/Props/Hallway/";
        private const string DR = "Assets/Prefabs/Rooms/Props/Dressing_Room/";
        private const string ST = "Assets/Prefabs/Rooms/Props/Stage/";
        private const string OR = "Assets/Prefabs/Rooms/Props/Other_Rooms/";
        private const string AU = "Assets/Prefabs/Rooms/Props/Auditorium/";

        private static readonly string[] Posters = { "SM_Poster", "SM_Poster_V2" };
        private static readonly string[] Frames = { "SM_Frame_V1", "SM_Frame_V2" };
        private static readonly string[] Carpets = { "SM_Carpet_01", "SM_Carpet_02", "SM_Carpet_03", "SM_Carpet_05", "SM_Carpet_06" };
        private static readonly string[] Litter = { "SM_Popcorn_Paper_Big", "SM_Popcorn_Paper_Small", "SM_Popcorn_Paper_Big_Amount", "SM_Popcorn_Paper_Small_Amount", "SM_Cup_Big", "SM_Cup_Small" };

        private enum Kit { Mural, Avisos, Caixas, Palco, Figurino, Descanso, Armario, Cadeiras, Lixo }

        // Weights per kit: ground floor (service) and upper floor (public).
        private static readonly Dictionary<Kit, (float ground, float upper)> Weights = new Dictionary<Kit, (float, float)>
        {
            { Kit.Mural, (1.2f, 2.6f) }, { Kit.Avisos, (1.4f, 0.5f) }, { Kit.Caixas, (1.6f, 0.3f) }, { Kit.Palco, (1.2f, 0.2f) },
            { Kit.Figurino, (0.8f, 0.1f) }, { Kit.Descanso, (0.6f, 1.6f) }, { Kit.Armario, (0.9f, 0f) }, { Kit.Cadeiras, (0.6f, 0.8f) },
            { Kit.Lixo, (0.7f, 1.3f) },
        };

        private class Ctx
        {
            public Transform Root, Decals;
            public System.Random Rnd;
            public readonly Dictionary<Kit, int> Count = new Dictionary<Kit, int>();
            public int Pieces, Stains;
            public float R(float a, float b) => a + (float)Rnd.NextDouble() * (b - a);
        }

        public static void Dress(PrototypeBuilder.Context ctx)
        {
            PrototypeLayout l = ctx.Layout;
            PrototypeCorridors c = ctx.Corridors;
            var d = new Ctx { Root = Group("Decoracao", ctx.Root).transform, Decals = Group("Decoracao_Manchas", ctx.Root).transform, Rnd = new System.Random(ctx.Level * 977) };

            // Places along a corridor where both walls are solid: away from crossings, doors, stairs and ducts.
            var blocked = new List<Vector3>();
            blocked.AddRange(c.Stairs.SelectMany(s => new[] { s.Top, s.Bottom }));
            blocked.AddRange(c.Ducts.SelectMany(du => new[] { du.A, du.B }));
            blocked.AddRange(c.DoorSpots.Select(ds => ds.pos));
            blocked.AddRange(l.Rooms.SelectMany(r => r.DoorPoints.Values));
            foreach (WPiece p in l.Pieces)
                foreach (WPiece o in l.Pieces.Where(o => o != p && o.Horizontal != p.Horizontal && p.Bar.Touching.Contains(o.Bar)))
                    blocked.Add(p.Horizontal ? new Vector3(o.Line, p.FloorA, p.Line) : new Vector3(p.Line, p.FloorA, o.Line));
            bool Free(Vector3 pos, float clearance) => blocked.All(b => Mathf.Abs(b.y - pos.y) > 1.5f || Vector2.Distance(new Vector2(b.x, b.z), new Vector2(pos.x, pos.z)) > clearance);
            bool NearFixture(Vector3 wallPoint) => c.WallFixtures.Any(f => Mathf.Abs(f.y - 2.05f - wallPoint.y) < 1f && Vector2.Distance(new Vector2(f.x, f.z), new Vector2(wallPoint.x, wallPoint.z)) < 1.3f);

            foreach (var kv in c.Flat)
            {
                WPiece p = kv.Key;
                if (p.Color == BarColor.Yellow) continue;
                float a = kv.Value.a, b = kv.Value.b;
                if (b - a < 5f) continue;
                Vector3 along = p.Horizontal ? Vector3.right : Vector3.forward;
                Vector3 across = p.Horizontal ? Vector3.forward : Vector3.right;
                bool upper = p.FloorA > 1f;
                float lastSide = d.Rnd.NextDouble() < 0.5 ? -1f : 1f;
                for (float t = a + 2.6f; t < b - 2.6f; t += d.R(2.4f, 3.8f))
                {
                    Vector3 floor = p.Horizontal ? new Vector3(t, p.FloorA, p.Line) : new Vector3(p.Line, p.FloorA, t);

                    // Damp and stains go anywhere, even near doors.
                    if (d.Rnd.NextDouble() < 0.42)
                        Stain(d, floor + across * d.R(-1.2f, 1.2f) + along * d.R(-0.8f, 0.8f), d.Rnd.NextDouble() < 0.25 ? PrototypeGrime.Kind.Poca : d.Rnd.NextDouble() < 0.3 ? PrototypeGrime.Kind.Poeira : PrototypeGrime.Kind.Mancha);
                    if (d.Rnd.NextDouble() < 0.22)
                    {
                        float s = d.Rnd.NextDouble() < 0.5 ? -1f : 1f;
                        PrototypeGrime.Place(PrototypeGrime.Kind.Escorrido, d.Decals, floor + across * s * (ClearWidth * 0.5f) + Vector3.up * (WallHeight - 0.95f), -across * s, d.R(0.8f, 1.5f), 1.9f, 0f);
                        d.Stains++;
                    }

                    if (!Free(floor, 3.0f)) continue;
                    float side = d.Rnd.NextDouble() < 0.7 ? -lastSide : lastSide; // mostly alternate sides
                    lastSide = side;
                    Vector3 inward = -across * side;
                    Vector3 wall = floor + across * side * (ClearWidth * 0.5f);
                    Kit kit = Pick(d, upper);
                    if ((kit == Kit.Mural || kit == Kit.Avisos) && NearFixture(wall)) kit = upper ? Kit.Descanso : Kit.Caixas;
                    Dress(d, kit, wall, inward, along, upper);
                    d.Count[kit] = d.Count.TryGetValue(kit, out int n) ? n + 1 : 1;

                    if (upper && d.Rnd.NextDouble() < 0.35)
                        Put(d, HW + Carpets[d.Rnd.Next(Carpets.Length)] + ".prefab", floor + along * d.R(-0.5f, 0.5f) + Vector3.up * 0.004f, Quaternion.LookRotation(along) * Quaternion.Euler(0f, d.R(-4f, 4f), 0f), Props, new Vector3(2.0f, 1f, 2.3f));
                }
            }

            // Exit signs over every stair foot and at the auditorium side doors: the way up is the way out.
            int signs = 0;
            foreach (var s in c.Stairs.Where(s => s.Exit))
            {
                Vector3 over = s.Bottom + Vector3.up * 2.62f - s.Dir * 0.02f;
                if (Put(d, HW + "SM_Exit_Light.prefab", over, Quaternion.LookRotation(-s.Dir, Vector3.up), Scenary) != null) signs++;
            }
            foreach (float x in new[] { -16.4f, 16.4f })
                if (Put(d, HW + "SM_Exit_Light.prefab", new Vector3(x, 2.62f, -1.85f), Quaternion.LookRotation(new Vector3(Mathf.Sign(x), 0f, 0f), Vector3.up), Scenary) != null) signs++;

            // Worth filming, risky to stop for: at duct mouths (the monster cannot follow, but you are cornered
            // until you crawl) and next to dead-end bathrooms.
            int audience = 0;
            string[] valuable = { "Sword", "SM_Crown", "Happy_Mask_Low", "Sad_Mask_Low" };
            var spots = new List<Vector3>();
            spots.AddRange(c.Ducts.Select(du => du.A.y < 1f ? du.A : du.B));
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
                var crate = Put(d, DR + "SM_Cardboard_Box.prefab", at, Quaternion.Euler(0f, audience * 37f, 0f), Scenary);
                float top = 0.57f;
                string prop = valuable[audience % valuable.Length];
                Quaternion rot = prop == "Sword" ? Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(0f, audience * 67f, 0f);
                var go = Place("Assets/Prefabs/Audience_Objects/" + prop + ".prefab", d.Root, at + Vector3.up * (top + (prop == "Sword" ? 0.03f : 0.0f)), rot);
                if (go != null) audience++;
            }

            // A framed portrait in the Bilheteria's side corridors: the foyer's showpiece (150 audience).
            WRoom bil = l.Hub(RoomKind.Bilheteria);
            foreach (var dp in bil.DoorPoints.Values.Where(p => Mathf.Abs(p.z - bil.Pivot.z - 2f) < 0.5f))
            {
                Vector3 outward = new Vector3(Mathf.Sign(dp.x - bil.Pivot.x), 0f, 0f);
                Vector3 at = dp + outward * 4f + Vector3.forward * (ClearWidth * 0.5f - 0.05f) + Vector3.up * 0.6f;
                var go = Place("Assets/Prefabs/Audience_Objects/SM_Sophisticated_Frame.prefab", d.Root, at, Quaternion.LookRotation(Vector3.back, Vector3.up));
                if (go != null) audience++;
            }

            MarkStatic(d.Root.gameObject);
            string kits = string.Join(", ", d.Count.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}"));
            ctx.Report.AppendLine($"  decoração: {d.Pieces} peças ({kits}), {d.Stains} manchas, {signs} placas de saída, {audience} objetos de audiência");
        }

        private static Kit Pick(Ctx d, bool upper)
        {
            float total = Weights.Values.Sum(w => upper ? w.upper : w.ground);
            float r = (float)d.Rnd.NextDouble() * total;
            foreach (var kv in Weights)
            {
                r -= upper ? kv.Value.upper : kv.Value.ground;
                if (r <= 0f) return kv.Key;
            }
            return Kit.Mural;
        }

        private static GameObject Put(Ctx d, string path, Vector3 at, Quaternion rot, int layer, Vector3? scale = null, bool collider = false, float colliderHeight = 99f)
        {
            var go = Place(path, d.Root, at, rot, scale);
            if (go == null) return null;
            SetLayerRecursive(go, layer);
            if (collider && go.GetComponentInChildren<Collider>(true) == null) FitBoxCollider(go, layer, colliderHeight);
            d.Pieces++;
            return go;
        }

        private static void Stain(Ctx d, Vector3 at, PrototypeGrime.Kind kind)
        {
            PrototypeGrime.Place(kind, d.Decals, at + Vector3.up * 0.002f, Vector3.up, d.R(0.9f, 2.2f), d.R(0.9f, 2.2f), d.R(0f, 360f));
            d.Stains++;
        }

        // Long side of a prop along the wall: local x along `along`.
        private static Quaternion AlongX(Vector3 along) => Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
        private static Quaternion Flat(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -90f); // a paper lying face up

        private static void Dress(Ctx d, Kit kit, Vector3 wall, Vector3 inward, Vector3 along, bool upper)
        {
            Vector3 up = Vector3.up;
            Quaternion onWall = FacingX(inward);
            float s = d.Rnd.NextDouble() < 0.5 ? -1f : 1f; // which way along the wall secondary pieces go
            switch (kit)
            {
                case Kit.Mural:
                {
                    // A poster or portrait, with notes and flyers stuck around it.
                    bool frame = upper && d.Rnd.NextDouble() < 0.55;
                    string main = frame ? Frames[d.Rnd.Next(Frames.Length)] : Posters[d.Rnd.Next(Posters.Length)];
                    Put(d, HW + main + ".prefab", wall + up * (frame ? 1.0f : d.R(1.15f, 1.35f)) + inward * 0.01f, onWall, Props);
                    int papers = d.Rnd.Next(1, 4);
                    for (int i = 0; i < papers; i++)
                        Put(d, HW + PrototypeFoyer.Paper(d.Rnd) + ".prefab", wall + along * s * d.R(0.6f, 1.2f) * (i % 2 == 0 ? 1 : -1) + up * d.R(1.1f, 1.8f) + inward * (0.012f + i * 0.003f), Quaternion.AngleAxis(d.R(-9f, 9f), inward) * onWall, Props);
                    if (d.Rnd.NextDouble() < 0.6)
                        Put(d, HW + (d.Rnd.NextDouble() < 0.5 ? "SM_Post_Its" : "SM_Post_Its_V2") + ".prefab", wall + along * s * d.R(0.7f, 1.0f) + up * d.R(1.2f, 1.5f) + inward * 0.05f, onWall, Props);
                    if (d.Rnd.NextDouble() < 0.4)
                        Put(d, HW + PrototypeFoyer.Paper(d.Rnd) + ".prefab", wall + inward * d.R(0.3f, 0.9f) + along * d.R(-0.8f, 0.8f) + up * 0.004f, Flat(d.R(0f, 360f)), Props);
                    break;
                }
                case Kit.Avisos:
                {
                    // Notice board crowded with call sheets and post-its.
                    Put(d, HW + "SM_Notice_Board.prefab", wall + up * 0.9f + inward * 0.005f, onWall, Props);
                    int papers = d.Rnd.Next(3, 7);
                    for (int i = 0; i < papers; i++)
                        Put(d, HW + PrototypeFoyer.Paper(d.Rnd) + ".prefab", wall + along * d.R(-1.05f, 1.05f) + up * d.R(1.15f, 2.0f) + inward * (0.058f + i * 0.002f), Quaternion.AngleAxis(d.R(-6f, 6f), inward) * onWall, Props);
                    for (int i = 0; i < 2; i++)
                        Put(d, HW + (i == 0 ? "SM_Post_Its" : "SM_Post_Its_V2") + ".prefab", wall + along * d.R(-0.7f, 0.7f) + up * d.R(1.3f, 1.9f) + inward * 0.1f, onWall, Props);
                    break;
                }
                case Kit.Caixas:
                {
                    // Boxes stacked against the wall: cover in a chase.
                    Vector3 at = wall + inward * 0.34f;
                    Put(d, DR + "SM_Cardboard_Box.prefab", at, AlongX(along) * Quaternion.Euler(0f, d.R(-8f, 8f), 0f), Scenary);
                    if (d.Rnd.NextDouble() < 0.7) Put(d, DR + "SM_Cardboard_Box.prefab", at + up * 0.57f + along * d.R(-0.08f, 0.08f), AlongX(along) * Quaternion.Euler(0f, d.R(-15f, 15f), 0f), Scenary);
                    if (d.Rnd.NextDouble() < 0.4) Put(d, DR + "SM_Cardboard_Small_Box.prefab", at + up * 1.14f, Quaternion.Euler(0f, d.R(0f, 360f), 0f), Scenary);
                    Put(d, DR + (d.Rnd.NextDouble() < 0.5 ? "SM_Cardboard_Small_Box" : "SM_Cardboard_Box") + ".prefab", at + along * s * 0.95f, AlongX(along) * Quaternion.Euler(0f, d.R(-25f, 25f), 0f), Scenary);
                    if (d.Rnd.NextDouble() < 0.35) Put(d, ST + "SM_Pallet_V1.prefab", wall + inward * 0.62f - along * s * 1.2f, Quaternion.Euler(0f, d.R(0f, 30f), 0f), Scenary);
                    break;
                }
                case Kit.Palco:
                {
                    // Stage gear pushed out of the way: a spotlight (sometimes knocked over), a speaker, a flight case.
                    bool fallen = d.Rnd.NextDouble() < 0.4;
                    Quaternion spot = Quaternion.LookRotation(Quaternion.AngleAxis(d.R(-40f, 40f), up) * inward);
                    if (fallen) Put(d, ST + "SM_Tripod_Spotilight_V1.prefab", wall + inward * 0.5f + up * 0.32f, Quaternion.LookRotation(along * s) * Quaternion.Euler(-88f, 0f, 0f), Scenary);
                    else Put(d, ST + (d.Rnd.NextDouble() < 0.5 ? "SM_Tripod_Spotilight_V1" : "SM_Tripod_SoundBox") + ".prefab", wall + inward * 0.5f, spot, Scenary);
                    Put(d, DR + "SM_Flight_Case_Big.prefab", wall + inward * 0.33f + along * s * 1.2f, AlongX(along), Scenary);
                    if (d.Rnd.NextDouble() < 0.6) Put(d, ST + "SM_SoundBox_Floor.prefab", wall + inward * 0.3f - along * s * 1.0f, FacingZ(inward) * Quaternion.Euler(0f, d.R(-20f, 20f), 0f), Scenary);
                    break;
                }
                case Kit.Figurino:
                {
                    // A costume rack left in the corridor, its trunk open with the hat on it.
                    Put(d, DR + "SM_Clothes_Rack.prefab", wall + inward * 0.36f, Quaternion.LookRotation(along), Scenary);
                    Vector3 trunk = wall + inward * 0.32f + along * s * 1.15f;
                    Put(d, DR + (d.Rnd.NextDouble() < 0.5 ? "SM_Costume_Box_Open" : "SM_Costume_Box") + ".prefab", trunk, AlongX(along), Scenary);
                    if (d.Rnd.NextDouble() < 0.6) Put(d, DR + "SM_Top_Hat.prefab", trunk + up * 0.58f, Quaternion.Euler(d.R(-5f, 5f), d.R(0f, 360f), 0f), Props);
                    if (d.Rnd.NextDouble() < 0.4) Put(d, OR + "SM_Mirror.prefab", wall + inward * 0.012f - along * s * 1.3f, FacingZ(inward), Props);
                    break;
                }
                case Kit.Descanso:
                {
                    // A bench with the remains of an interval: popcorn, cups, a bin (sometimes knocked over).
                    Vector3 at = wall + inward * 0.36f;
                    Put(d, HW + (d.Rnd.NextDouble() < 0.5 ? "SM_Small_Bench" : "SM_Big_Bench") + ".prefab", at, Quaternion.LookRotation(along), Scenary);
                    for (int i = 0; i < d.Rnd.Next(1, 3); i++) LitterAt(d, at + up * 0.52f + along * d.R(-0.4f, 0.4f), false);
                    for (int i = 0; i < d.Rnd.Next(2, 5); i++) LitterAt(d, wall + inward * d.R(0.4f, 1.3f) + along * d.R(-1.2f, 1.2f), true);
                    if (d.Rnd.NextDouble() < 0.5)
                    {
                        bool tipped = d.Rnd.NextDouble() < 0.5;
                        Put(d, OR + "SM_Trash_Can_V1.prefab", wall + inward * (tipped ? 0.6f : 0.28f) + along * s * 1.1f + (tipped ? up * 0.2f : Vector3.zero), tipped ? Quaternion.LookRotation(along * s) * Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity, tipped ? Props : Scenary);
                    }
                    break;
                }
                case Kit.Armario:
                {
                    bool shelf = d.Rnd.NextDouble() < 0.4;
                    if (shelf) Put(d, OR + "SM_Metal_Shelf_V1.prefab", wall + inward * 0.25f, FacingZ(inward), Scenary);
                    else
                    {
                        Put(d, OR + "SM_Iron_Locker.prefab", wall + inward * 0.25f, FacingZ(inward), Scenary);
                        if (d.Rnd.NextDouble() < 0.6) Put(d, OR + "SM_Iron_Locker.prefab", wall + inward * 0.25f + along * 1.2f * s, FacingZ(inward), Scenary);
                    }
                    if (d.Rnd.NextDouble() < 0.5) Put(d, DR + "SM_Cardboard_Small_Box.prefab", wall + inward * 0.6f - along * s * 0.9f, Quaternion.Euler(0f, d.R(0f, 360f), 0f), Scenary);
                    break;
                }
                case Kit.Cadeiras:
                {
                    // Auditorium seats carried out and dumped: one folded against the wall, one on its back.
                    Put(d, AU + "SM_Chair_Closed.prefab", wall + inward * 0.02f, FacingZ(inward), Scenary);
                    Put(d, AU + "SM_Chair_Closed.prefab", wall + inward * 0.02f + along * 0.78f * s, FacingZ(inward) * Quaternion.Euler(0f, d.R(-6f, 6f), 0f), Scenary);
                    if (d.Rnd.NextDouble() < 0.7) Put(d, AU + "SM_Chair.prefab", wall + inward * 0.95f - along * s * 0.6f + up * 0.02f, Quaternion.LookRotation(Quaternion.AngleAxis(d.R(0f, 360f), up) * Vector3.forward) * Quaternion.Euler(-90f, 0f, 0f), Scenary);
                    break;
                }
                case Kit.Lixo:
                {
                    for (int i = 0; i < d.Rnd.Next(4, 9); i++) LitterAt(d, wall + inward * d.R(0.25f, 1.6f) + along * d.R(-1.6f, 1.6f), true);
                    for (int i = 0; i < d.Rnd.Next(1, 4); i++)
                        Put(d, HW + PrototypeFoyer.Paper(d.Rnd) + ".prefab", wall + inward * d.R(0.3f, 1.4f) + along * d.R(-1.5f, 1.5f) + up * 0.004f, Flat(d.R(0f, 360f)), Props);
                    break;
                }
            }
        }

        private static void LitterAt(Ctx d, Vector3 at, bool mayLie)
        {
            string item = Litter[d.Rnd.Next(Litter.Length)];
            bool lying = mayLie && d.Rnd.NextDouble() < 0.5;
            Quaternion rot = Quaternion.Euler(0f, d.R(0f, 360f), 0f) * (lying ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);
            Put(d, AU + item + ".prefab", at + (lying ? Vector3.up * 0.08f : Vector3.zero), rot, Props);
        }
    }
}
