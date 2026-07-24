using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoProjectionTests
    {
        // 1 degree of latitude on the WGS84 sphere model: PI/180 * 6378137 m.
        const float MetersPerDegree = 111319.49f;

        [Test]
        public void Project_OneDegreeLat_FromEquatorCenter_IsMetersPerDegree()
        {
            var proj = new LocalProjection(0.0, 0.0);
            var v = proj.Project(new GeoPoint(0.0, 1.0));
            Assert.AreEqual(0f, v.X, 0.01f);
            Assert.AreEqual(MetersPerDegree, v.Y, 0.5f);
        }

        [Test]
        public void Project_OneDegreeLon_At60North_IsHalved_ByCosLat()
        {
            var proj = new LocalProjection(0.0, 60.0);
            var v = proj.Project(new GeoPoint(1.0, 60.0));
            Assert.AreEqual(MetersPerDegree * 0.5f, v.X, 0.5f); // cos(60°) = 0.5
            Assert.AreEqual(0f, v.Y, 0.01f);
        }

        [Test]
        public void Project_CenterMapsToOrigin()
        {
            var proj = new LocalProjection(-0.55, 47.47); // ~Angers
            var v = proj.Project(new GeoPoint(-0.55, 47.47));
            Assert.AreEqual(0f, v.X, 1e-3f);
            Assert.AreEqual(0f, v.Y, 1e-3f);
        }

        [Test]
        public void Unproject_RoundTrips_WithinCentimeters()
        {
            var proj = new LocalProjection(-0.55, 47.47);
            var p0 = new GeoPoint(-0.404, 47.559); // Angers NE-ish corner
            var v = proj.Project(p0);
            var p1 = proj.Unproject(v);
            Assert.AreEqual(p0.Lon, p1.Lon, 1e-6); // ~0.1 m in lon at this latitude
            Assert.AreEqual(p0.Lat, p1.Lat, 1e-6);
        }
    }
}
