using System.Collections.Generic;
using UnityEngine;

namespace Prototypes.Editor
{
    /// <summary>
    /// Closes the see-through slits at wall corners. The Sophisticated hallway pieces have a 0.356 m
    /// bounding box, but their wall slab is only local x 0.076..0.279 (the skirting and crown are the
    /// full width), and <see cref="PrototypeGeometry.Wall"/> lines the pieces up box to box. Every
    /// corner was left with an ~8 cm diagonal gap you could see the void through.
    ///
    /// <para>Each open wall end that butts into a perpendicular wall is lengthened until its slab stops
    /// 1 cm short of the partner slab's far face (short, so the end face never sits coplanar with it
    /// and flickers); small end-to-end gaps along one line are closed. Ends that already run on into
    /// a collinear piece are left alone, and the door pieces are never stretched (their opening would
    /// stretch with them).</para>
    /// </summary>
    public static class PrototypeWallSeal
    {
        private const float Slab0 = 0.076f, Slab1 = 0.279f, Margin = 0.01f, Reach = 0.3f;

        private class Piece
        {
            public Transform T;
            public float Z0, Z1, X0, X1;   // local length range and slab range
            public bool Door, Column;
            public Vector2 Min, Max;       // slab footprint in world x/z
            public bool AlongX;
            public float Floor;
            public Vector3 U;              // world direction of local +z
            public readonly float[] Ext = new float[2];
            public readonly bool[] Continued = new bool[2];
        }

        public static string Run(Transform root)
        {
            var pieces = Collect(root);
            int collinear = 0, corners = 0, tees = 0;

            // Collinear continuations first: an end that runs on into the next piece is not open.
            foreach (Piece a in pieces)
            foreach (Piece b in pieces)
            {
                if (a == b || a.AlongX != b.AlongX || Mathf.Abs(a.Floor - b.Floor) > 0.5f) continue;
                int ax = a.AlongX ? 0 : 1, lat = 1 - ax;
                float latOverlap = Mathf.Min(a.Max[lat], b.Max[lat]) - Mathf.Max(a.Min[lat], b.Min[lat]);
                if (latOverlap < 0.15f) continue;
                if (!(b.Max[ax] > a.Max[ax] + 0.005f && b.Min[ax] <= a.Max[ax] + Reach && b.Min[ax] > a.Min[ax] + 0.01f)) continue;

                int ea = EndIndex(a, +1), eb = EndIndex(b, -1);
                a.Continued[ea] = true;
                b.Continued[eb] = true;
                float gap = b.Min[ax] - a.Max[ax];
                if (gap <= 0.001f) continue;

                // Closed from whichever side is a plain wall.
                Piece k = a.Door || (a.Column && !b.Door) ? b : a;
                if (k.Door) continue;
                int ek = k == a ? ea : eb;
                k.Ext[ek] = Mathf.Max(k.Ext[ek], gap + 0.002f);
                collinear++;
            }

            // Perpendicular junctions: L corners and T joins.
            for (int i = 0; i < pieces.Count; i++)
            for (int j = i + 1; j < pieces.Count; j++)
            {
                Piece p = pieces[i], q = pieces[j];
                if (p.AlongX == q.AlongX || Mathf.Abs(p.Floor - q.Floor) > 0.5f) continue;
                if (p.Min.x > q.Max.x + Reach || q.Min.x > p.Max.x + Reach || p.Min.y > q.Max.y + Reach || q.Min.y > p.Max.y + Reach) continue;
                if (p.Min.x < q.Max.x && q.Min.x < p.Max.x && p.Min.y < q.Max.y && q.Min.y < p.Max.y) continue; // slabs already overlap

                Piece a = p.AlongX ? p : q, b = p.AlongX ? q : p; // a along x, b along z
                float ea = Needed(a.Min.x, a.Max.x, b.Min.x, b.Max.x, out int aSign);
                float eb = Needed(b.Min.y, b.Max.y, a.Min.y, a.Max.y, out int bSign);
                if (ea > 0f && a.Continued[EndIndex(a, aSign)]) ea = 0f;
                if (eb > 0f && b.Continued[EndIndex(b, bSign)]) eb = 0f;

                // Only a junction if the two lengthened slabs meet.
                Vector2 aMin = a.Min, aMax = a.Max, bMin = b.Min, bMax = b.Max;
                if (aSign > 0) aMax.x += ea; else if (aSign < 0) aMin.x -= ea;
                if (bSign > 0) bMax.y += eb; else if (bSign < 0) bMin.y -= eb;
                if (!(aMin.x < bMax.x && bMin.x < aMax.x && aMin.y < bMax.y && bMin.y < aMax.y)) continue;
                if (ea <= 0f && eb <= 0f) continue;
                if ((ea > 0f && a.Door) || (eb > 0f && b.Door)) continue;

                if (ea > 0f) { int e = EndIndex(a, aSign); a.Ext[e] = Mathf.Max(a.Ext[e], ea); }
                if (eb > 0f) { int e = EndIndex(b, bSign); b.Ext[e] = Mathf.Max(b.Ext[e], eb); }
                if (ea > 0f && eb > 0f) corners++; else tees++;
            }

            int changed = 0;
            foreach (Piece w in pieces)
            {
                if (w.Ext[0] <= 0f && w.Ext[1] <= 0f) continue;
                Transform t = w.T;
                float sz = t.localScale.z;
                Vector3 p0 = t.position;
                Vector3 e0 = p0 + w.U * (w.Z0 * sz) - w.U * w.Ext[0];
                Vector3 e1 = p0 + w.U * (w.Z1 * sz) + w.U * w.Ext[1];
                float szNew = Vector3.Dot(e1 - e0, w.U) / (w.Z1 - w.Z0);
                Vector3 pNew = e0 - w.U * (w.Z0 * szNew);
                t.position = new Vector3(pNew.x, p0.y, pNew.z);
                t.localScale = new Vector3(t.localScale.x, t.localScale.y, szNew);
                changed++;
            }
            return $"  cantos de parede vedados: {changed} peças ({corners} em L, {tees} em T, {collinear} emendas)";
        }

        /// <summary>How far a piece spanning [min,max] must grow towards a partner spanning [pMin,pMax] to reach 1 cm short of its far face.</summary>
        private static float Needed(float min, float max, float pMin, float pMax, out int sign)
        {
            sign = 0;
            if (min < pMin - 0.01f && max > pMax + 0.01f) return 0f; // runs straight past the partner
            if ((min + max) < (pMin + pMax))
            {
                if (max < pMax - Margin - 0.001f && max >= pMin - Reach) { sign = +1; return (pMax - Margin) - max; }
            }
            else if (min > pMin + Margin + 0.001f && min <= pMax + Reach) { sign = -1; return min - (pMin + Margin); }
            return 0f;
        }

        /// <summary>Which local end (0 = local z0, 1 = local z1) faces the world direction sign along the piece's axis.</summary>
        private static int EndIndex(Piece p, int sign)
        {
            Vector3 dir = p.AlongX ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign);
            return Vector3.Dot(dir, p.U) > 0f ? 1 : 0;
        }

        private static List<Piece> Collect(Transform root)
        {
            var list = new List<Piece>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                var p = new Piece { T = mf.transform };
                switch (mf.sharedMesh.name)
                {
                    case "Parede_Detalhada_2m": p.Z0 = -2f; p.Z1 = 0f; p.X0 = Slab0; p.X1 = Slab1; break;
                    case "Parede_Detalhada_4m": p.Z0 = -4f; p.Z1 = 0f; p.X0 = Slab0; p.X1 = Slab1; break;
                    case "Parede_Detalhada_Coluna_4m.001": p.Z0 = -4f; p.Z1 = 0f; p.X0 = Slab0; p.X1 = Slab1; p.Column = true; break;
                    case "Porta_Dupla_Detalhada_Final": p.Z0 = 0f; p.Z1 = 4f; p.X0 = -Slab1; p.X1 = -Slab0; p.Door = true; break;
                    case "Parede_Porta_Detalhada.001": p.Z0 = -2f; p.Z1 = 0f; p.X0 = -Slab1; p.X1 = -Slab0; p.Door = true; break;
                    default: continue;
                }
                Transform t = p.T;
                float yaw = t.eulerAngles.y;
                bool square = Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Round(yaw / 90f) * 90f)) < 0.5f
                              && Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.x, 0f)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.z, 0f)) < 0.5f;
                bool scaled = t.localScale.z > 0f && t.lossyScale.x > 0f && (t.parent == null || (t.parent.lossyScale - Vector3.one).sqrMagnitude < 1e-4f);
                if (!square || !scaled) continue;

                Matrix4x4 m = t.localToWorldMatrix;
                p.Min = new Vector2(float.MaxValue, float.MaxValue);
                p.Max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector3 local in new[] { new Vector3(p.X0, 1.5f, p.Z0), new Vector3(p.X1, 1.5f, p.Z0), new Vector3(p.X0, 1.5f, p.Z1), new Vector3(p.X1, 1.5f, p.Z1) })
                {
                    Vector3 w = m.MultiplyPoint3x4(local);
                    p.Min = Vector2.Min(p.Min, new Vector2(w.x, w.z));
                    p.Max = Vector2.Max(p.Max, new Vector2(w.x, w.z));
                }
                p.Floor = t.position.y;
                p.U = t.TransformDirection(Vector3.forward);
                p.U.y = 0f;
                p.U.Normalize();
                // From the length axis, not the footprint: offcuts shorter than the slab is thick exist.
                p.AlongX = Mathf.Abs(p.U.x) > Mathf.Abs(p.U.z);
                list.Add(p);
            }
            return list;
        }
    }
}
