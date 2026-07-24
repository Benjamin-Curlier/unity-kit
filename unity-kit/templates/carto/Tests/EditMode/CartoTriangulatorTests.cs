using System.Collections.Generic;
using Carto.Core;
using NUnit.Framework;
using V2 = System.Numerics.Vector2;

namespace Snake2D.Tests.Carto
{
    public class CartoTriangulatorTests
    {
        static float TriangleAreaSum(List<V2> v, List<int> idx)
        {
            float s = 0;
            for (int i = 0; i < idx.Count; i += 3)
            {
                V2 a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];
                s += System.Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * 0.5f;
            }
            return s;
        }

        static (List<V2>, List<int>, int) Run(V2[] outer, V2[][] holes)
        {
            var verts = new List<V2>();
            var idx = new List<int>();
            Assert.IsTrue(PolygonTriangulator.Triangulate(outer, holes, verts, idx, out var dropped));
            Assert.AreEqual(0, idx.Count % 3);
            return (verts, idx, dropped);
        }

        [Test]
        public void UnitSquare_TwoTriangles_AreaOne()
        {
            var (v, idx, _) = Run(new[] { new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1) }, null);
            Assert.AreEqual(6, idx.Count);
            Assert.AreEqual(1f, TriangleAreaSum(v, idx), 1e-4f);
        }

        [Test]
        public void ClockwiseInput_Normalized_SameArea()
        {
            var (v, idx, _) = Run(new[] { new V2(0, 1), new V2(1, 1), new V2(1, 0), new V2(0, 0) }, null);
            Assert.AreEqual(1f, TriangleAreaSum(v, idx), 1e-4f);
        }

        [Test]
        public void ConcaveL_Shape_AreaThree()
        {
            // L covering 3 unit squares: (0,0)-(2,0)-(2,1)-(1,1)-(1,2)-(0,2)
            var outer = new[] { new V2(0, 0), new V2(2, 0), new V2(2, 1), new V2(1, 1), new V2(1, 2), new V2(0, 2) };
            var (v, idx, _) = Run(outer, null);
            Assert.AreEqual(3f, TriangleAreaSum(v, idx), 1e-4f);
        }

        [Test]
        public void SquareWithSquareHole_AreaIsDifference()
        {
            var outer = new[] { new V2(0, 0), new V2(4, 0), new V2(4, 4), new V2(0, 4) };
            var hole = new[] { new V2(1, 1), new V2(3, 1), new V2(3, 3), new V2(1, 3) };
            var (v, idx, dropped) = Run(outer, new[] { hole });
            Assert.AreEqual(16f - 4f, TriangleAreaSum(v, idx), 1e-3f);
            Assert.AreEqual(0, dropped);
        }

        [Test]
        public void TwoDisjointHoles_AreaIsDifference_NoneDropped()
        {
            var outer = new[] { new V2(0, 0), new V2(8, 0), new V2(8, 8), new V2(0, 8) };
            var h1 = new[] { new V2(1, 1), new V2(3, 1), new V2(3, 3), new V2(1, 3) };
            var h2 = new[] { new V2(5, 5), new V2(7, 5), new V2(7, 7), new V2(5, 7) };
            var (v, idx, dropped) = Run(outer, new[] { h1, h2 });
            Assert.AreEqual(64f - 4f - 4f, TriangleAreaSum(v, idx), 1e-3f);
            Assert.AreEqual(0, dropped);
        }

        [Test]
        public void HoleSharingVertexWithOuter_DroppedAndCounted_NoCorruption()
        {
            // Real cadastral holes can touch the outer ring. A coincident vertex cannot be
            // bridged without a self-touching polygon (ear-clip area inflation), so the
            // hole is dropped (counted) and the feature fills over it.
            var outer = new[] { new V2(0, 0), new V2(4, 0), new V2(4, 4), new V2(0, 4) };
            var hole = new[] { new V2(0, 0), new V2(1, 0.5f), new V2(0.5f, 1) };
            var verts = new List<V2>();
            var idx = new List<int>();
            var ok = PolygonTriangulator.Triangulate(outer, new[] { hole }, verts, idx, out var dropped);
            Assert.IsTrue(ok);
            Assert.AreEqual(1, dropped);
            Assert.AreEqual(0, idx.Count % 3);
            Assert.AreEqual(16f, TriangleAreaSum(verts, idx), 1e-3f);
        }

        [Test]
        public void DegenerateInput_ReturnsFalse()
        {
            var verts = new List<V2>();
            var idx = new List<int>();
            Assert.IsFalse(PolygonTriangulator.Triangulate(new[] { new V2(0, 0), new V2(1, 1) }, null, verts, idx));
        }
    }
}
