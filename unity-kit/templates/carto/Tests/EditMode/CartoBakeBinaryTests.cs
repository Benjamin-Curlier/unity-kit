using System.IO;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoBakeBinaryTests
    {
        static CartoMapData BakeSample() =>
            CartoMapData.Bake(PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream(CartoPlaniReaderTests.SampleXml)), "sample");

        [Test]
        public void Bake_CenterIsOrigin_KnownPointProjectsToExpectedMeters()
        {
            var data = BakeSample();
            Assert.AreEqual(-0.535, data.CenterLon, 1e-9);
            Assert.AreEqual(47.475, data.CenterLat, 1e-9);
            // road point (-0.51, 47.51): dLon=0.025, dLat=0.035
            // x = 0.025 * cos(47.475°) * 111319.49 ≈ 1881.0 ; y = 0.035 * 111319.49 ≈ 3896.2
            var p = data.Roads[0].Points[1];
            Assert.AreEqual(1881.0f, p.X, 2f);
            Assert.AreEqual(3896.2f, p.Y, 2f);
        }

        [Test]
        public void Bake_AllLayersCarriedOver_WithHoles()
        {
            var data = BakeSample();
            Assert.AreEqual(1, data.Roads.Count);
            Assert.AreEqual(1, data.Railways.Count);
            Assert.AreEqual(1, data.Vegetation.Count);
            Assert.AreEqual(1, data.Vegetation[0].Holes.Length);
            Assert.AreEqual(1, data.Water.Count);
            Assert.AreEqual(1, data.Buildings.Count);
            Assert.AreEqual(0, data.Bridges.Count);
        }

        [Test]
        public void Bake_BoundsCoverAllGeometry()
        {
            var data = BakeSample();
            Assert.Less(data.BoundsMin.X, data.BoundsMax.X);
            Assert.Less(data.BoundsMin.Y, data.BoundsMax.Y);
            foreach (var r in data.Roads)
                foreach (var p in r.Points)
                {
                    Assert.GreaterOrEqual(p.X, data.BoundsMin.X);
                    Assert.LessOrEqual(p.Y, data.BoundsMax.Y);
                }
        }

        [Test]
        public void SaveLoad_RoundTrip_IsLossless()
        {
            var data = BakeSample();
            var ms = new MemoryStream();
            data.Save(ms);
            ms.Position = 0;
            var back = CartoMapData.Load(ms);

            Assert.AreEqual("sample", back.SourceName);
            Assert.AreEqual(data.CenterLon, back.CenterLon, 0.0);
            Assert.AreEqual(data.Roads.Count, back.Roads.Count);
            // whole-struct value equality — all fields of each Info survive, not a sample
            Assert.AreEqual(data.Roads[0].Info, back.Roads[0].Info);
            Assert.AreEqual(data.Railways[0].Info, back.Railways[0].Info);
            Assert.AreEqual(data.Vegetation[0].Info, back.Vegetation[0].Info);
            Assert.AreEqual(data.Water[0].Info, back.Water[0].Info);
            Assert.AreEqual(data.Buildings[0].Info, back.Buildings[0].Info);
            CollectionAssert.AreEqual(data.Roads[0].Points, back.Roads[0].Points);       // exact float bits
            CollectionAssert.AreEqual(data.Vegetation[0].Outer, back.Vegetation[0].Outer);
            Assert.AreEqual(data.Vegetation[0].Holes.Length, back.Vegetation[0].Holes.Length);
            CollectionAssert.AreEqual(data.Vegetation[0].Holes[0], back.Vegetation[0].Holes[0]);
            Assert.AreEqual(data.BoundsMax.X, back.BoundsMax.X);
        }

        [Test]
        public void Load_WrongMagic_Throws()
        {
            var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Assert.Throws<System.FormatException>(() => CartoMapData.Load(ms));
        }

        [Test]
        public void Load_CorruptCounts_ThrowFormatException_NotOOM()
        {
            var ms = new MemoryStream();
            BakeSample().Save(ms);
            var bytes = ms.ToArray();
            // First layer count (Roads) offset: magic(4)+version(2)+"sample"(1+6)+center(16)+bounds(16) = 45
            const int roadsCountOffset = 45;

            var huge = (byte[])bytes.Clone();
            System.BitConverter.GetBytes(int.MaxValue).CopyTo(huge, roadsCountOffset);
            Assert.Throws<System.FormatException>(() => CartoMapData.Load(new MemoryStream(huge)));

            var negative = (byte[])bytes.Clone();
            System.BitConverter.GetBytes(-5).CopyTo(negative, roadsCountOffset);
            Assert.Throws<System.FormatException>(() => CartoMapData.Load(new MemoryStream(negative)));
        }
    }
}
