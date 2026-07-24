namespace Carto.Core
{
    /// <summary>Geographic coordinate, WGS84 decimal degrees.</summary>
    public readonly struct GeoPoint
    {
        public readonly double Lon;
        public readonly double Lat;

        public GeoPoint(double lon, double lat)
        {
            Lon = lon;
            Lat = lat;
        }

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:F7}, {1:F7})", Lon, Lat);
    }
}
