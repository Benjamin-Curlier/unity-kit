using System.Collections.Generic;
using System.Numerics;

namespace Carto.Core
{
    /// <summary>
    /// Ear-clipping triangulation with hole support (holes bridged into the outer
    /// ring before clipping). Import-time / load-time use only — O(n²), fine for
    /// BD TOPO-scale polygons (tens to low thousands of vertices).
    /// </summary>
    public static class PolygonTriangulator
    {
        /// <summary>
        /// Triangulates outer (any winding) minus holes. Appends the merged vertex
        /// list to outVerts and triangle indices (CCW) to outIndices.
        /// Returns false on degenerate input; both lists are cleared first either way.
        /// droppedHoles counts holes that were degenerate or could not be bridged and
        /// were filled over — callers must surface it, never swallow it.
        /// Near-degenerate sliver triangles are expected and harmless in a flat fill.
        /// Complexity ~O(n²) realistic, O(n³) pathological — import/load-time use only.
        /// </summary>
        public static bool Triangulate(Vector2[] outer, Vector2[][] holes, List<Vector2> outVerts, List<int> outIndices, out int droppedHoles)
        {
            outVerts.Clear();
            outIndices.Clear();
            droppedHoles = 0;
            if (outer == null || outer.Length < 3) return false;

            var ring = new List<Vector2>(outer);
            if (SignedArea(ring) < 0) ring.Reverse(); // outer CCW

            if (holes != null && holes.Length > 0)
            {
                var holeList = new List<List<Vector2>>();
                foreach (var h in holes)
                {
                    if (h == null || h.Length < 3) { droppedHoles++; continue; }
                    var hl = new List<Vector2>(h);
                    if (SignedArea(hl) > 0) hl.Reverse(); // holes CW
                    holeList.Add(hl);
                }
                holeList.Sort((a, b) => MaxX(b).CompareTo(MaxX(a))); // rightmost hole first
                foreach (var h in holeList) ring = MergeHole(ring, h, ref droppedHoles);
            }

            return EarClip(ring, outVerts, outIndices);
        }

        /// <summary>Convenience overload when the caller does not track dropped holes.</summary>
        public static bool Triangulate(Vector2[] outer, Vector2[][] holes, List<Vector2> outVerts, List<int> outIndices)
            => Triangulate(outer, holes, outVerts, outIndices, out _);

        static float SignedArea(List<Vector2> ring)
        {
            float s = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                s += a.X * b.Y - b.X * a.Y;
            }
            return s * 0.5f;
        }

        static float MaxX(List<Vector2> ring)
        {
            float m = ring[0].X;
            for (int i = 1; i < ring.Count; i++) if (ring[i].X > m) m = ring[i].X;
            return m;
        }

        static List<Vector2> MergeHole(List<Vector2> ring, List<Vector2> hole, ref int droppedHoles)
        {
            // A hole vertex sitting exactly on the ring (outer or an already-merged hole)
            // cannot be bridged without producing a self-touching polygon — any bridge
            // choice still leaves that coincident point visited twice at non-adjacent
            // positions, which the ear-clipper's bridge-duplicate exception can mistake
            // for a legitimate ear and overcount area. Drop it rather than risk that.
            for (int i = 0; i < hole.Count; i++)
                for (int j = 0; j < ring.Count; j++)
                    if (Same(hole[i], ring[j])) { droppedHoles++; return ring; }

            int hi = 0;
            for (int i = 1; i < hole.Count; i++) if (hole[i].X > hole[hi].X) hi = i;
            var h = hole[hi];

            var order = new List<int>(ring.Count);
            for (int i = 0; i < ring.Count; i++) order.Add(i);
            order.Sort((a, b) => (ring[a] - h).LengthSquared().CompareTo((ring[b] - h).LengthSquared()));

            foreach (var ri in order)
            {
                if (!SegmentClear(ring, hole, h, ring[ri])) continue;
                // splice: ..ring[ri], hole[hi..] wrapped back to hole[hi], ring[ri], rest of ring
                var merged = new List<Vector2>(ring.Count + hole.Count + 2);
                for (int i = 0; i <= ri; i++) merged.Add(ring[i]);
                for (int i = 0; i <= hole.Count; i++) merged.Add(hole[(hi + i) % hole.Count]);
                merged.Add(ring[ri]);
                for (int i = ri + 1; i < ring.Count; i++) merged.Add(ring[i]);
                return merged;
            }
            droppedHoles++;
            return ring; // unbridgeable hole — dropped (counted), rendering-grade fallback
        }

        static bool SegmentClear(List<Vector2> ring, List<Vector2> hole, Vector2 a, Vector2 b)
        {
            for (int i = 0; i < ring.Count; i++)
                if (ProperIntersect(a, b, ring[i], ring[(i + 1) % ring.Count])) return false;
            for (int i = 0; i < hole.Count; i++)
                if (ProperIntersect(a, b, hole[i], hole[(i + 1) % hole.Count])) return false;
            return true;
        }

        static bool ProperIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            if (Same(p1, q1) || Same(p1, q2) || Same(p2, q1) || Same(p2, q2)) return false;
            float d1 = Cross(q2 - q1, p1 - q1);
            float d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1);
            float d4 = Cross(p2 - p1, q2 - p1);
            return ((d1 > 0) != (d2 > 0)) && ((d3 > 0) != (d4 > 0));
        }

        // Effectively exact-equality at map scale (1e-6 m << float ULP at 10 km) — used only
        // on bit-identical bridge duplicates; do NOT reuse as a geometric proximity tolerance.
        static bool Same(Vector2 a, Vector2 b) => (a - b).LengthSquared() < 1e-12f;
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        static bool EarClip(List<Vector2> poly, List<Vector2> outVerts, List<int> outIndices)
        {
            outVerts.AddRange(poly);
            var idx = new List<int>(poly.Count);
            for (int i = 0; i < poly.Count; i++) idx.Add(i);

            int guard = poly.Count * poly.Count + 16;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = outVerts[i0], b = outVerts[i1], c = outVerts[i2];
                    if (Cross(b - a, c - a) <= 1e-12f) continue; // reflex or collinear — not an ear

                    bool contains = false;
                    for (int j = 0; j < idx.Count; j++)
                    {
                        int p = idx[j];
                        if (p == i0 || p == i1 || p == i2) continue;
                        var q = outVerts[p];
                        // bridge duplicates share coordinates with corners — not blockers
                        if (Same(q, a) || Same(q, b) || Same(q, c)) continue;
                        if (PointInTriangle(q, a, b, c)) { contains = true; break; }
                    }
                    if (contains) continue;

                    outIndices.Add(i0); outIndices.Add(i1); outIndices.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // no ear found (self-intersecting input) — partial result
            }

            if (idx.Count == 3)
            {
                outIndices.Add(idx[0]); outIndices.Add(idx[1]); outIndices.Add(idx[2]);
                return true;
            }
            return idx.Count < 3;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0; // CCW triangle, edge-inclusive
        }
    }
}
