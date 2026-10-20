using System.Linq;
using UnityEngine;
using static Prototypes.Editor.PrototypeGeometry;

namespace Prototypes.Editor
{
    /// <summary>
    /// Furnishes the Bilheteria (the shell the corridors build): a theatre foyer left as it was the
    /// night it closed. Ticket booth on the north-west, snack bar on the north-east, sofas and chairs
    /// along the side walls, a stopped renovation in the south corners. Carpets run the main path
    /// (south doors from the auditorium to the north door that leads to the Escape) and the exit sign
    /// marks that door. A chandelier hangs over a display stand in the middle; another lies on the
    /// floor, the landmark you see first coming in from the auditorium.
    /// </summary>
    public static class PrototypeFoyer
    {
        private const string BO = "Assets/Prefabs/Rooms/Props/Box_Office/";
        private const string AU = "Assets/Prefabs/Rooms/Props/Auditorium/";
        private const string HW = "Assets/Prefabs/Rooms/Props/Hallway/";
        private const string DR = "Assets/Prefabs/Rooms/Props/Dressing_Room/";
        private const string ST = "Assets/Prefabs/Rooms/Props/Stage/";
        private const string OR = "Assets/Prefabs/Rooms/Props/Other_Rooms/";

        public static void Dress(PrototypeBuilder.Context ctx)
        {
            var shell = ctx.Corridors?.Shells.FirstOrDefault(s => s.Room.Room.Kind == RoomKind.Bilheteria);
            if (shell == null) return;
            var root = Group("Bilheteria_Mobilia", ctx.Root).transform;
            var decals = Group("Bilheteria_Manchas", ctx.Root).transform;
            var rnd = new System.Random(ctx.Level * 131 + 7);
            Vector3 c0 = new Vector3(shell.Area.center.x, shell.Y, shell.Area.center.y);
            float half = shell.Area.width * 0.5f, top = shell.Height;
            Vector3 P(float x, float z, float y = 0f) => c0 + new Vector3(x, y, z);
            Quaternion South = Quaternion.LookRotation(Vector3.back), North = Quaternion.LookRotation(Vector3.forward);
            Quaternion East = Quaternion.LookRotation(Vector3.right), West = Quaternion.LookRotation(Vector3.left);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            int count = 0;

            GameObject Put(string path, Vector3 at, Quaternion rot, int layer, bool collider = false, float colliderHeight = 99f, Vector3? scale = null)
            {
                var go = Place(path, root, at, rot, scale);
                if (go == null) return null;
                SetLayerRecursive(go, layer);
                if (collider && go.GetComponentInChildren<Collider>(true) == null) FitBoxCollider(go, layer, colliderHeight);
                count++;
                return go;
            }

            // ---- ticket booth (north-west): counter facing the room, the clerks' chairs behind it.
            Put(BO + "SM_Ticket_Counter.prefab", P(-2.8f, 10.2f), South, Scenary, true, 1.2f);
            foreach (float x in new[] { -4.6f, -7.1f, -9.6f })
                Put(BO + "SM_Chair_Foyer.prefab", P(x, 11.3f), South * Quaternion.Euler(0f, R(-12f, 12f), 0f), Scenary, true, 1f);
            Put(BO + "SM_Tickets_Sign_New.prefab", P(-6.46f, half, 2.9f), South, Props);
            Put(BO + "SM_Chair_Foyer.prefab", P(-5.2f, 8.2f, 0.32f), Quaternion.Euler(90f, 35f, 0f), Scenary, true, 0.7f); // knocked over
            for (int i = 0; i < 7; i++)
                Put(HW + Paper(rnd) + ".prefab", P(R(-11f, -2f), R(6.5f, 9f), 0.004f), Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(0f, 0f, -90f), Props);

            // ---- snack bar (north-east): back counter with the machines, front counter with the till.
            Put(BO + "SM_Cabinet.prefab", P(12.0f, 12.3f), South, Scenary, true);
            Put(BO + "SM_Cabinet.prefab", P(7.5f, 12.3f), South, Scenary, true);
            Put(BO + "SM_Popcorn_Machine.prefab", P(10.8f, 11.7f, 1.10f), South, Props);
            Put(BO + "SM_Soda_Machine.prefab", P(9.6f, 11.7f, 1.10f), South, Props);
            Put(BO + "SM_Popcorn_Machine.prefab", P(5.0f, 11.7f, 1.10f), South, Props);
            Put(BO + "SM_Soda_Machine.prefab", P(6.2f, 11.7f, 1.10f), South * Quaternion.Euler(0f, 18f, 0f), Props);
            Put(BO + "SM_Floor_Cabinet.prefab", P(4.7f, 9.6f), South, Scenary, true);
            Put(BO + "SM_Floor_Cabinet_Player.prefab", P(7.7f, 9.6f), South, Scenary, true);
            Put(BO + "SM_Cash_Register.prefab", P(6.2f, 9.1f, 1.04f), South, Props);
            Put(BO + "SM_Menu_Board_Big_1.prefab", P(4.2f, half, 2.4f), South, Props);
            Put(BO + "SM_Menu_Board_Big_2.prefab", P(5.0f, half, 2.4f), South, Props);
            Put(BO + "SM_Menu_Board_Small_1.prefab", P(10.0f, half, 2.35f), South, Props);
            Put(BO + "SM_Menu_Board_Small_2.prefab", P(10.8f, half, 2.35f), South, Props);
            Put(BO + "SM_Snacks_Sign_New.prefab", P(7.87f, half, 3.3f), South, Props);
            // Popcorn and cups everywhere in front of it: the last session was never cleaned up.
            for (int i = 0; i < 16; i++) Litter(rnd, P(R(3f, 11.5f), R(5.5f, 8.2f)));

            // ---- west wall: sofas under portraits.
            foreach (float z in new[] { -2.2f, -5.6f, 6.2f })
            {
                bool moved = z < -5f;
                var at = P(-half + 1.0f + (moved ? 0.6f : 0f), z);
                Put(BO + "SM_Sofa.prefab", at, East * Quaternion.Euler(0f, moved ? 22f : 0f, 0f), Scenary, true, 1f);
                Put(HW + (z > 0 ? "SM_Frame_V2" : "SM_Frame_V1") + ".prefab", P(-half, z, 1.35f), FacingX(Vector3.right), Props);
            }
            Put(OR + "SM_Trash_Can_V1.prefab", P(-half + 0.45f, -3.9f), Quaternion.identity, Scenary);
            Put(OR + "SM_Trash_Can_V1.prefab", P(-half + 1.6f, 4.6f, 0.2f), Quaternion.Euler(0f, 30f, 90f), Props); // tipped over
            for (int i = 0; i < 4; i++) Litter(rnd, P(R(-11.5f, -9f), R(3.6f, 5.6f)));

            // ---- east wall: a row of chairs (one missing, one on the floor) under posters.
            float[] chairs = { -1.4f, -2.2f, -3.0f, -3.8f, -5.4f, -6.2f };
            foreach (float z in chairs)
                Put(BO + "SM_Chair_Foyer.prefab", P(half - 0.45f, z), West * Quaternion.Euler(0f, R(-6f, 6f), 0f), Scenary, true, 1f);
            Put(BO + "SM_Chair_Foyer.prefab", P(half - 1.6f, -4.6f, 0.32f), Quaternion.Euler(90f, -70f, 0f), Scenary, true, 0.7f);
            foreach (float z in new[] { -2.0f, -5.0f, -8.0f })
                Put(HW + (z < -6f ? "SM_Poster_V2" : "SM_Poster") + ".prefab", P(half, z, 1.25f), FacingX(Vector3.left), Props);
            // Boxes from a delivery nobody unpacked, and the cart that brought them.
            Put(DR + "SM_Cardboard_Box.prefab", P(half - 0.5f, 5.0f), Quaternion.Euler(0f, 90f, 0f), Scenary);
            Put(DR + "SM_Cardboard_Box.prefab", P(half - 0.5f, 5.0f, 0.57f), Quaternion.Euler(0f, 78f, 0f), Scenary);
            Put(DR + "SM_Cardboard_Box.prefab", P(half - 0.5f, 6.0f), Quaternion.Euler(0f, 95f, 0f), Scenary);
            Put(DR + "SM_Cardboard_Small_Box.prefab", P(half - 1.3f, 5.6f), Quaternion.Euler(0f, 20f, 0f), Scenary);
            Put(ST + "SM_Dolly_Cart.prefab", P(half - 1.1f, 7.4f), Quaternion.Euler(0f, 200f, 0f), Scenary);

            // ---- south wall between the auditorium doors: portraits and a bench.
            foreach (float x in new[] { -3.0f, 0f, 3.0f })
                Put(HW + "SM_Frame_V1.prefab", P(x, -half, 1.3f), FacingX(Vector3.forward), Props);
            Put(HW + "SM_Big_Bench.prefab", P(0f, -half + 0.45f), East, Scenary);
            Put(HW + "SM_Small_Bench.prefab", P(-3.2f, -half + 0.45f), East, Scenary);
            Litter(rnd, P(0.3f, -half + 0.45f, 0.52f));

            // ---- south corners: a renovation that stopped (scaffold, pallets, paint).
            Put(ST + "SM_Layher_Tower.prefab", P(10.4f, -10.4f), Quaternion.Euler(0f, 8f, 0f), Scenary);
            PlaceCentered(ST + "ConjuntoPallets.prefab", root, P(-10.5f, -10.6f), Quaternion.Euler(0f, 15f, 0f));
            Put(DR + "SM_Cardboard_Box.prefab", P(-9.4f, -11.6f), Quaternion.Euler(0f, 5f, 0f), Scenary);
            Put(DR + "SM_Cardboard_Small_Box.prefab", P(-9.4f, -11.6f, 0.57f), Quaternion.Euler(0f, 40f, 0f), Scenary);
            PrototypeGrime.Place(PrototypeGrime.Kind.Mancha, decals, P(10.2f, -9.4f, 0.002f), Vector3.up, 2.4f, 2.0f, 30f);

            // ---- centre: display stand under the chandelier (a spotlight on it went through both, so it is gone).
            Vector3 stand = P(0f, -3f);
            Put(HW + "SM_Carpet_04.prefab", stand + Vector3.up * 0.004f, Quaternion.identity, DefaultLayer, false, 99f, new Vector3(3.4f, 1f, 3.4f));
            Put(BO + "SM_Stand.prefab", stand, Quaternion.identity, Scenary, true);
            Put(AU + "SM_Chandelier.prefab", stand + Vector3.up * top, Quaternion.identity, Props, false, 99f, Vector3.one * 0.45f);
            var chand = Place("Assets/Prefabs/Managers/SabotageObjects/SabotageLight.prefab", root, stand + Vector3.up * (top - 1.2f), Quaternion.identity);
            if (chand != null)
            {
                var l = chand.GetComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.78f, 0.5f); l.intensity = 4.2f; l.range = 13f;
                l.shadows = LightShadows.Soft; l.lightmapBakeType = LightmapBakeType.Mixed;
                ctx.ExtraSabotage.Add(chand);
            }

            // The fallen one: first thing you see coming in from the west auditorium door.
            var fallen = Put(AU + "SM_Chandelier.prefab", P(-5.2f, -6.8f, 0.55f), Quaternion.Euler(82f, 35f, 0f), Scenary, false, 99f, Vector3.one * 0.45f);
            if (fallen != null) FitBoxCollider(fallen, Scenary);
            PrototypeGrime.Place(PrototypeGrime.Kind.Poca, decals, P(-5.6f, -7.4f, 0.002f), Vector3.up, 2.6f, 2.2f, 10f);
            for (int i = 0; i < 5; i++)
                Put(HW + Paper(rnd) + ".prefab", P(R(-7.5f, -3f), R(-9f, -5f), 0.004f), Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(0f, 0f, -90f), Props);

            // ---- carpets on the main path: from both auditorium doors north, and across from west to east.
            string[] carpets = { "SM_Carpet_01", "SM_Carpet_02", "SM_Carpet_03", "SM_Carpet_05", "SM_Carpet_06" };
            int ci = 0;
            foreach (float x in new[] { -6.95f, 6.95f })
                for (float z = -10.9f; z < -4.5f; z += 2.45f)
                    Put(HW + carpets[ci++ % carpets.Length] + ".prefab", P(x, z, 0.004f), Quaternion.Euler(0f, R(-2f, 2f), 0f), DefaultLayer, false, 99f, new Vector3(2.0f, 1f, 2.4f));
            for (float x = -10.6f; x <= 10.7f; x += 2.65f)
                if (Mathf.Abs(x) > 1.5f)
                    Put(HW + carpets[ci++ % carpets.Length] + ".prefab", P(x, 2f, 0.004f), Quaternion.Euler(0f, 90f + R(-2f, 2f), 0f), DefaultLayer, false, 99f, new Vector3(2.0f, 1f, 2.6f));
            for (float z = 6.2f; z < 11.5f; z += 2.45f)
                Put(HW + carpets[ci++ % carpets.Length] + ".prefab", P(0f, z, 0.004f), Quaternion.identity, DefaultLayer, false, 99f, new Vector3(2.0f, 1f, 2.4f));

            // ---- the way on: exit sign over the north door (to the Escape).
            Put(HW + "SM_Exit_Light.prefab", P(0f, half - 0.05f, 2.62f), South, Props);

            // ---- light: four lamps (one dead) and the chandelier; damp on the walls, stains on the floor.
            int k = 0;
            foreach (var o in new[] { new Vector2(-6.5f, -6.5f), new Vector2(6.5f, -6.5f), new Vector2(-6.5f, 6.5f), new Vector2(6.5f, 6.5f) })
                CeilingLight(root, P(o.x, o.y) - Vector3.up * shell.Y, shell.Y + top, new Color(1f, 0.82f, 0.6f), 4.6f, 11f, k++ != 2, ctx.ExtraSabotage);
            foreach (var (pos, n) in new[] { (P(-half, -9.0f), Vector3.right), (P(half, 9.6f), Vector3.left), (P(4.0f, -half), Vector3.forward), (P(-9.5f, half), Vector3.back), (P(half, -10.2f), Vector3.left) })
                PrototypeGrime.Place(PrototypeGrime.Kind.Escorrido, decals, pos + Vector3.up * (top - 1.0f), n, R(1.0f, 1.6f), 2.0f, 0f);
            for (int i = 0; i < 9; i++)
                PrototypeGrime.Place(i % 3 == 0 ? PrototypeGrime.Kind.Poeira : PrototypeGrime.Kind.Mancha, decals, P(R(-11f, 11f), R(-11f, 11f), 0.002f), Vector3.up, R(1.2f, 2.8f), R(1.2f, 2.8f), R(0f, 360f));

            MarkStatic(root.gameObject);
            ctx.Report.AppendLine($"  bilheteria: {count} peças de mobília e decoração");

            void Litter(System.Random r, Vector3 at)
            {
                string[] items = { "SM_Popcorn_Paper_Big", "SM_Popcorn_Paper_Small", "SM_Popcorn_Paper_Big_Amount", "SM_Popcorn_Paper_Small_Amount", "SM_Cup_Big", "SM_Cup_Small" };
                string item = items[r.Next(items.Length)];
                bool lying = r.NextDouble() < 0.45;
                Quaternion rot = Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f) * (lying ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);
                MakeLoose(Put(AU + item + ".prefab", at + (lying ? Vector3.up * 0.08f : Vector3.zero), rot, Props));
            }
        }

        private static readonly string[] Papers = { "SM_Paper_12x16", "SM_Paper_16x9_V2", "SM_Paper_25x30", "SM_Paper_28x40_V2", "SM_Paper_32x18", "SM_Paper_45x63", "SM_Paper_28x40" };
        public static string Paper(System.Random r) => Papers[r.Next(Papers.Length)];
    }
}
