using System;
using System.Numerics;

namespace Carto.Core
{
    /// <summary>
    /// Tangent-plane equirectangular projection centered on (CenterLon, CenterLat).
    /// x = east meters, y = north meters. Double math, rounded to float32 output.
    /// E–W scale/shape distortion grows with |lat − CenterLat| (rel. error ≈ tan(lat0)·Δlat):
    /// at the Angers extent (±0.084° of latitude) that is ≈0.16 % (~15 m at the far corners).
    /// Absolute agreement with UTM ground truth is looser (spherical model) — but features
    /// and raster corners share the same center, so relative registration stays exact.
    /// Deterministic only per single offline bake: Math.Cos is not bit-identical across
    /// platforms — never run this per-client in a lockstep sim. Swap this class behind the
    /// same API if survey-grade fidelity is ever needed.
    /// </summary>
    public sealed class LocalProjection
    {
        public const double EarthRadius = 6378137.0; // WGS84 semi-major axis

        public double CenterLon { get; }
        public double CenterLat { get; }

        readonly double _mPerDegLat;
        readonly double _mPerDegLon;

        public LocalProjection(double centerLon, double centerLat)
        {
            CenterLon = centerLon;
            CenterLat = centerLat;
            _mPerDegLat = Math.PI / 180.0 * EarthRadius;
            _mPerDegLon = _mPerDegLat * Math.Cos(centerLat * Math.PI / 180.0);
        }

        public Vector2 Project(GeoPoint p) => new Vector2(
            (float)((p.Lon - CenterLon) * _mPerDegLon),
            (float)((p.Lat - CenterLat) * _mPerDegLat));

        public GeoPoint Unproject(Vector2 v) => new GeoPoint(
            CenterLon + v.X / _mPerDegLon,
            CenterLat + v.Y / _mPerDegLat);
    }
}
