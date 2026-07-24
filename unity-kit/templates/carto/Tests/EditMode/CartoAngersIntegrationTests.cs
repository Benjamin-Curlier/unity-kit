using System.IO;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoAngersIntegrationTests
    {
        const string DataDir = @"C:\Users\bencu\unityProjects\snake-unity-kit\Carto\carte angers";
        static string XmlPath => Path.Combine(DataDir, "Angers2.xml");
        static string GeoPath => Path.Combine(DataDir, "angersZUB.geo");

        [Test]
        public void ImportAngers2_EndToEnd_ParseBakeRoundTrip()
        {
            if (!File.Exists(XmlPath)) Assert.Ignore("Angers dataset not present on this machine");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            PlaniMap map;
            using (var fs = File.OpenRead(XmlPath)) map = PlaniXmlReader.Read(fs);
            TestContext.WriteLine($"parse: {sw.Elapsed.TotalSeconds:F1}s, warnings: {map.Warnings.Count}");

            Assert.Greater(map.Roads.Count, 1000, "Angers2 should contain thousands of roads");
            Assert.Less(map.LimWest, map.LimEast);
            Assert.That(map.CenterLat, Is.InRange(47.0, 48.0), "Angers sits at ~47.47°N");

            sw.Restart();
            var baked = CartoMapData.Bake(map, "Angers2");
            TestContext.WriteLine($"bake: {sw.Elapsed.TotalSeconds:F1}s");
            var span = baked.BoundsMax - baked.BoundsMin;
            Assert.That(span.X, Is.InRange(1000f, 40000f), "map should span kilometers east-west");
            Assert.That(span.Y, Is.InRange(1000f, 40000f), "map should span kilometers north-south");

            sw.Restart();
            var ms = new MemoryStream();
            baked.Save(ms);
            ms.Position = 0;
            var back = CartoMapData.Load(ms);
            TestContext.WriteLine($"binary round-trip: {sw.Elapsed.TotalSeconds:F1}s, {ms.Length / (1024 * 1024)} MB");
            Assert.AreEqual(baked.Roads.Count, back.Roads.Count);
            Assert.AreEqual(baked.Vegetation.Count, back.Vegetation.Count);
            Assert.AreEqual(baked.Buildings.Count, back.Buildings.Count);
        }

        [Test]
        public void AngersGeoSidecar_Parses()
        {
            if (!File.Exists(GeoPath)) Assert.Ignore("Angers dataset not present on this machine");
            GeoReference geo;
            using (var fs = File.OpenRead(GeoPath)) geo = GeoReference.Read(fs);
            Assert.AreEqual(1.5057104143025681, geo.PixelMetreX, 1e-9);
            Assert.AreEqual(12825, geo.DimensionImageX);
            Assert.AreEqual(12544, geo.DimensionImageY);
            Assert.That(geo.WidthMeters, Is.InRange(19000.0, 19700.0));
        }
    }
}
