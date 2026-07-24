using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SysV2 = System.Numerics.Vector2;

namespace Carto.Unity
{
    /// <summary>
    /// Flat 2D mesh construction in the XY plane (z = 0), 1 unit = 1 m.
    /// All emitted triangles wind clockwise in XY — visible to a camera at −Z
    /// looking +Z with back-face culling on (see CartoMeshBuilderTests).
    /// </summary>
    public static class CartoMeshBuilder
    {
        public static Mesh BuildPolylines(IReadOnlyList<(SysV2[] points, float width)> lines, string name)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var (points, width) in lines)
            {
                if (points == null || points.Length < 2) continue;
                float half = Mathf.Max(width, 0.5f) * 0.5f;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var a = new Vector2(points[i].X, points[i].Y);
                    var b = new Vector2(points[i + 1].X, points[i + 1].Y);
                    var d = b - a;
                    float len = d.magnitude;
                    if (len < 1e-4f) continue;
                    var n = new Vector2(-d.y, d.x) / len * half;
                    int v0 = verts.Count;
                    verts.Add(a - n); verts.Add(a + n); verts.Add(b - n); verts.Add(b + n);
                    // CW pairs for a camera looking +Z:
                    tris.Add(v0); tris.Add(v0 + 1); tris.Add(v0 + 2);
                    tris.Add(v0 + 1); tris.Add(v0 + 3); tris.Add(v0 + 2);
                }
            }
            return ToMesh(verts, tris, name);
        }

        public static Mesh BuildPolygons(IReadOnlyList<(SysV2[] outer, SysV2[][] holes)> areas, string name)
            => BuildPolygons(areas, name, out _, out _);

        public static Mesh BuildPolygons(IReadOnlyList<(SysV2[] outer, SysV2[][] holes)> areas, string name,
            out int droppedHoles, out int droppedPolygons)
        {
            droppedHoles = 0;
            droppedPolygons = 0;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var polyVerts = new List<SysV2>();
            var polyIdx = new List<int>();
            foreach (var (outer, holes) in areas)
            {
                if (!Carto.Core.PolygonTriangulator.Triangulate(outer, holes, polyVerts, polyIdx, out var dropped))
                {
                    droppedPolygons++;
                    continue;
                }
                droppedHoles += dropped;
                int baseIndex = verts.Count;
                foreach (var p in polyVerts) verts.Add(new Vector3(p.X, p.Y, 0f));
                // triangulator emits CCW → reverse to CW for visibility
                for (int i = 0; i < polyIdx.Count; i += 3)
                {
                    tris.Add(baseIndex + polyIdx[i]);
                    tris.Add(baseIndex + polyIdx[i + 2]);
                    tris.Add(baseIndex + polyIdx[i + 1]);
                }
            }
            return ToMesh(verts, tris, name);
        }

        static Mesh ToMesh(List<Vector3> verts, List<int> tris, string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0); // calculateBounds defaults to true — no extra pass needed
            return mesh;
        }
    }
}
