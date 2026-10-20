using System.Collections.Generic;
using System.Linq;
using Monster;
using UnityEditor;
using UnityEngine;

namespace Prototypes.Editor
{
    /// <summary>
    /// The monster's patrol sectors, made for the level that was built rather than inherited from
    /// Game: the copied scene arrives with Game's sectors, whose names and places mean nothing in a
    /// new map (on Game1 two were off the NavMesh and several sat on islands).
    /// - one per room slot, at the room's centre (the rooms only get floor once SpawnRooms has spawned
    ///   them and rebuilt the NavMesh; PatrolSector samples at run time, so that is fine);
    /// - the hub: stage, seats, backstage, dressing room, Bilheteria, bathrooms;
    /// - the corridors, covered greedily: each sector goes where it covers the most corridor not yet
    ///   covered, so junctions get one and long runs are split.
    /// </summary>
    public static class PrototypeSectors
    {
        private const float CorridorRadius = 15f;
        private const float CoverRadius = 18f;
        private const int MaxCorridorSectors = 30;

        public static string Build(PrototypeBuilder.Context ctx)
        {
            PrototypeLayout l = ctx.Layout;
            GameObject root = ctx.Find("Sectors") ?? new GameObject("Sectors");

            // Keep the gizmo colour the scene already uses (a solid sphere, so it must stay faint).
            Color color = new Color(1f, 0.3f, 0.3f, 0.12f);
            var old = root.GetComponentsInChildren<PatrolSector>(true);
            if (old.Length > 0) color = new SerializedObject(old[0]).FindProperty("patrolRadiusColor").colorValue;
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var sectors = new List<(string name, Vector3 at, float radius)>
            {
                ("Sector_Stage", new Vector3(0f, 1.5f, -8f), 6f),
                ("Sector_Auditorium", new Vector3(-0.1f, 3.3f, 14f), 11f),
            };

            foreach (var s in ctx.Corridors.Stairs.Where(s => !s.Exit))
                sectors.Add(("Sector_Backstage", s.Bottom + s.Dir * 2.5f, 4f));

            WRoom dressing = l.Rooms.First(r => r.Room.Kind == RoomKind.Dressing);
            sectors.Add(("Sector_DressingRoom", new Vector3(dressing.Footprint.center.x, dressing.FloorY, dressing.Footprint.center.y), 4f));

            var bil = ctx.Corridors.Shells.FirstOrDefault(s => s.Room.Room.Kind == RoomKind.Bilheteria);
            if (bil != null) sectors.Add(("Sector_Bilheteria", new Vector3(bil.Area.center.x, bil.Y, bil.Area.center.y), 10f));

            int k = 1;
            foreach (WRoom b in l.Rooms.Where(r => r.Room.Kind == RoomKind.Bathroom))
                sectors.Add(($"Sector_Bathroom_{k++:00}", b.Pivot + Quaternion.Euler(0f, b.Yaw, 0f) * new Vector3(-3.98f, 0f, -0.09f), 5f));

            // Room centres: the 14.7 m rooms sit 1 m off their pivot on both axes.
            foreach (Transform slot in ctx.RoomSlots)
                sectors.Add(("Sector_" + slot.name, slot.position + slot.rotation * new Vector3(-1f, 0f, -1f), 6f));

            int corridors = CoverCorridors(ctx, sectors);

            var created = new List<PatrolSector>();
            foreach (var (name, at, radius) in sectors)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = at;
                var ps = go.AddComponent<PatrolSector>();
                var so = new SerializedObject(ps);
                so.FindProperty("patrolSectorRadius").floatValue = radius;
                so.FindProperty("patrolRadiusColor").colorValue = color;
                so.ApplyModifiedPropertiesWithoutUndo();
                created.Add(ps);
            }

            var wander = ctx.Find("Monster")?.GetComponent<MonsterWander>();
            if (wander != null)
            {
                var so = new SerializedObject(wander);
                var list = so.FindProperty("allSectors");
                list.arraySize = created.Count;
                for (int i = 0; i < created.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = created[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return $"  setores de patrulha: {created.Count} ({ctx.RoomSlots.Count} salas, {corridors} de corredor){(wander == null ? " — PROBLEMA: MonsterWander não encontrado" : "")}";
        }

        private static int CoverCorridors(PrototypeBuilder.Context ctx, List<(string name, Vector3 at, float radius)> sectors)
        {
            var points = new List<Vector3>();
            foreach (var kv in ctx.Corridors.Flat)
            {
                WPiece p = kv.Key;
                if (p.Color == BarColor.Yellow) continue;
                for (float t = kv.Value.a + 1f; t <= kv.Value.b - 1f; t += 2f)
                    points.Add(p.Horizontal ? new Vector3(t, p.FloorA, p.Line) : new Vector3(p.Line, p.FloorA, t));
            }

            bool Covers(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < 1f && Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) <= CoverRadius;

            var uncovered = new List<Vector3>(points);
            int n = 0;
            while (uncovered.Count > 0 && n < MaxCorridorSectors)
            {
                Vector3 best = points.OrderByDescending(p => uncovered.Count(u => Covers(p, u))).First();
                uncovered.RemoveAll(u => Covers(best, u));
                sectors.Add(($"Sector_Corridor_{++n:00}", best, CorridorRadius));
            }
            return n;
        }
    }
}
