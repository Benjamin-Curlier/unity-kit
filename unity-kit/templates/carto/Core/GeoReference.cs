using System.Globalization;
using System.IO;
using System.Xml;

namespace Carto.Core
{
    /// <summary>
    /// Georeference sidecar (.geo) for a same-named raster (.tif/.gif):
    /// pixel size in meters, image dimensions, and the four corner coordinates.
    /// </summary>
    public sealed class GeoReference
    {
        public double PixelMetreX, PixelMetreY;
        public int DimensionImageX, DimensionImageY;
        public int Echelle; // producer writes meters-per-pixel * 10000; informational
        public GeoPoint CornerNW, CornerNE, CornerSE, CornerSW;

        public double WidthMeters => PixelMetreX * DimensionImageX;
        public double HeightMeters => PixelMetreY * DimensionImageY;

        public static GeoReference Read(Stream stream)
        {
            var g = new GeoReference();
            double lonNO = 0, latNO = 0, lonNE = 0, latNE = 0;
            double lonSE = 0, latSE = 0, lonSO = 0, latSO = 0;

            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
            using (var r = XmlReader.Create(stream, settings))
            {
                r.MoveToContent();
                if (r.IsEmptyElement) { return g; }
                r.ReadStartElement(); // enter <GeoReference>
                while (r.NodeType == XmlNodeType.Element)
                {
                    string name = r.Name;
                    if (!IsKnownField(name)) { r.Skip(); continue; } // unknown (incl. nested) → next sibling
                    string text = r.ReadElementContentAsString(); // consumes element, lands on next sibling
                    switch (name)
                    {
                        case "PixelMetreX": g.PixelMetreX = D(text); break;
                        case "PixelMetreY": g.PixelMetreY = D(text); break;
                        case "DimensionImageX": g.DimensionImageX = (int)D(text); break;
                        case "DimensionImageY": g.DimensionImageY = (int)D(text); break;
                        case "Echelle": g.Echelle = (int)D(text); break;
                        case "LongitudeNO": lonNO = D(text); break;
                        case "LatitudeNO": latNO = D(text); break;
                        case "LongitudeNE": lonNE = D(text); break;
                        case "LatitudeNE": latNE = D(text); break;
                        case "LongitudeSE": lonSE = D(text); break;
                        case "LatitudeSE": latSE = D(text); break;
                        case "LongitudeSO": lonSO = D(text); break;
                        case "LatitudeSO": latSO = D(text); break;
                    }
                }
            }

            g.CornerNW = new GeoPoint(lonNO, latNO);
            g.CornerNE = new GeoPoint(lonNE, latNE);
            g.CornerSE = new GeoPoint(lonSE, latSE);
            g.CornerSW = new GeoPoint(lonSO, latSO);
            return g;
        }

        static bool IsKnownField(string name)
        {
            switch (name)
            {
                case "PixelMetreX": case "PixelMetreY":
                case "DimensionImageX": case "DimensionImageY":
                case "Echelle":
                case "LongitudeNO": case "LatitudeNO":
                case "LongitudeNE": case "LatitudeNE":
                case "LongitudeSE": case "LatitudeSE":
                case "LongitudeSO": case "LatitudeSO":
                    return true;
                default:
                    return false;
            }
        }

        static double D(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;
    }
}
