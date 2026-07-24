using System.Collections.Generic;
using Carto.Unity;
using NUnit.Framework;
using UnityEngine;
using SysV2 = System.Numerics.Vector2;

namespace Snake2D.Tests.Carto
{
    public class CartoMeshBuilderTests
    {
        static void AssertAllTrianglesClockwise(Mesh mesh)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                float cross = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                Assert.Less(cross, 0f, "triangle " + i / 3 + " must be CW (visible to the 2D camera)");
            }
        }

        [Test]
        public void BuildPolylines_SingleSegment_QuadWithWidth()
        {
            var lines = new List<(SysV2[] points, float width)>
            {
                (new[] { new SysV2(0, 0), new SysV2(10, 0) }, 2f)
            };
            var mesh = CartoMeshBuilder.BuildPolylines(lines, "test");
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            // width 2 → offsets ±1 in Y for a horizontal segment
            Assert.AreEqual(-1f, mesh.bounds.min.y, 1e-4f);
            Assert.AreEqual(1f, mesh.bounds.max.y, 1e-4f);
            Assert.AreEqual(10f, mesh.bounds.max.x, 1e-4f);
            AssertAllTrianglesClockwise(mesh);
        }

        [Test]
        public void BuildPolylines_ZeroLengthSegments_Skipped()
        {
            var p = new SysV2(3, 3);
            var lines = new List<(SysV2[] points, float width)> { (new[] { p, p, p }, 5f) };
            var mesh = CartoMeshBuilder.BuildPolylines(lines, "test");
            Assert.AreEqual(0, mesh.vertexCount);
        }

        [Test]
        public void BuildPolygons_UnitSquare_VisibleWinding()
        {
            var areas = new List<(SysV2[] outer, SysV2[][] holes)>
            {
                (new[] { new SysV2(0, 0), new SysV2(1, 0), new SysV2(1, 1), new SysV2(0, 1) }, null)
            };
            var mesh = CartoMeshBuilder.BuildPolygons(areas, "test");
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            AssertAllTrianglesClockwise(mesh);
        }

        [Test]
        public void BuildPolygons_ManyAreas_IndicesOffsetCorrectly()
        {
            var square = new[] { new SysV2(0, 0), new SysV2(1, 0), new SysV2(1, 1), new SysV2(0, 1) };
            var far = new[] { new SysV2(10, 10), new SysV2(11, 10), new SysV2(11, 11), new SysV2(10, 11) };
            var mesh = CartoMeshBuilder.BuildPolygons(
                new List<(SysV2[] outer, SysV2[][] holes)> { (square, null), (far, null) }, "test");
            Assert.AreEqual(8, mesh.vertexCount);
            Assert.AreEqual(12, mesh.triangles.Length);
            foreach (var idx in mesh.triangles) Assert.Less(idx, 8);
            AssertAllTrianglesClockwise(mesh);
        }

        [Test]
        public void BuildPolygons_ReportsDroppedGeometry()
        {
            var good = new[] { new SysV2(0, 0), new SysV2(1, 0), new SysV2(1, 1), new SysV2(0, 1) };
            var degenerate = new[] { new SysV2(0, 0), new SysV2(1, 1) };
            var mesh = CartoMeshBuilder.BuildPolygons(
                new List<(SysV2[] outer, SysV2[][] holes)> { (good, null), (degenerate, null) }, "test",
                out var droppedHoles, out var droppedPolygons);
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(0, droppedHoles);
            Assert.AreEqual(1, droppedPolygons);
        }

        [Test]
        public void BuildPolylines_Above65535Vertices_SwitchesToUInt32()
        {
            // 20,000 one-segment lines → 80,000 verts: crosses the 16-bit index boundary —
            // the one Angers-scale behavior this layer owns.
            var lines = new List<(SysV2[] points, float width)>(20000);
            for (int i = 0; i < 20000; i++)
                lines.Add((new[] { new SysV2(i, 0), new SysV2(i, 1) }, 1f));
            var mesh = CartoMeshBuilder.BuildPolylines(lines, "big");
            Assert.AreEqual(80000, mesh.vertexCount);
            Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt32, mesh.indexFormat);
            int maxIndex = 0;
            foreach (var idx in mesh.triangles) if (idx > maxIndex) maxIndex = idx;
            Assert.Greater(maxIndex, 65535);
            Assert.Less(maxIndex, 80000);
        }
    }
}
