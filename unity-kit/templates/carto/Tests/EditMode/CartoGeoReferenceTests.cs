using System.IO;
using System.Text;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoGeoReferenceTests
    {
        const string SampleGeo =
            "<?xml version='1.0' encoding='utf-8'?>" +
            "<GeoReference><PixelMetreX>1.5057104143025681</PixelMetreX>" +
            "<PixelMetreY>1.5057104143025402</PixelMetreY>" +
            "<DimensionImageX>12825</DimensionImageX><DimensionImageY>12544</DimensionImageY>" +
            "<Echelle>15057</Echelle>" +
            "<LongitudeNO>-0.6603954426382074</LongitudeNO><LatitudeNO>47.56499839262511</LatitudeNO>" +
            "<LongitudeNE>-0.40392536331497525</LongitudeNE><LatitudeNE>47.55947681859172</LatitudeNE>" +
            "<LongitudeSE>-0.41227943000535916</LongitudeSE><LatitudeSE>47.389699733997716</LatitudeSE>" +
            "<LongitudeSO>-0.6679261259036828</LongitudeSO><LatitudeSO>47.39518877466679</LatitudeSO>" +
            "</GeoReference>";

        static Stream AsStream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

        [Test]
        public void Read_ParsesAllFields()
        {
            var geo = GeoReference.Read(AsStream(SampleGeo));
            Assert.AreEqual(1.5057104143025681, geo.PixelMetreX, 1e-12);
            Assert.AreEqual(1.5057104143025402, geo.PixelMetreY, 1e-12);
            Assert.AreEqual(12825, geo.DimensionImageX);
            Assert.AreEqual(12544, geo.DimensionImageY);
            Assert.AreEqual(15057, geo.Echelle);
            Assert.AreEqual(-0.6603954426382074, geo.CornerNW.Lon, 1e-12);
            Assert.AreEqual(47.56499839262511, geo.CornerNW.Lat, 1e-12);
            Assert.AreEqual(-0.40392536331497525, geo.CornerNE.Lon, 1e-12);
            Assert.AreEqual(47.389699733997716, geo.CornerSE.Lat, 1e-12);
            Assert.AreEqual(-0.6679261259036828, geo.CornerSW.Lon, 1e-12);
            Assert.AreEqual(47.55947681859172, geo.CornerNE.Lat, 1e-12);
            Assert.AreEqual(-0.41227943000535916, geo.CornerSE.Lon, 1e-12);
            Assert.AreEqual(47.39518877466679, geo.CornerSW.Lat, 1e-12);
            Assert.AreEqual(1.5057104143025681 * 12825, geo.WidthMeters, 1e-6);
            Assert.AreEqual(1.5057104143025402 * 12544, geo.HeightMeters, 1e-6);
        }

        [Test]
        public void Read_MissingFields_DefaultToZero_NoThrow()
        {
            var geo = GeoReference.Read(AsStream("<GeoReference><PixelMetreX>2.0</PixelMetreX></GeoReference>"));
            Assert.AreEqual(2.0, geo.PixelMetreX, 1e-12);
            Assert.AreEqual(0, geo.DimensionImageX);
            Assert.AreEqual(0.0, geo.CornerNW.Lon, 1e-12);
        }

        [Test]
        public void Read_UnknownNestedElementsAndComments_AreSkipped()
        {
            var geo = GeoReference.Read(AsStream(
                "<GeoReference><!-- comment --><Foo><Bar/></Foo>" +
                "<PixelMetreX>2.0</PixelMetreX><!-- c2 --><DimensionImageX>10</DimensionImageX>" +
                "</GeoReference>"));
            Assert.AreEqual(2.0, geo.PixelMetreX, 1e-12);
            Assert.AreEqual(10, geo.DimensionImageX);
            Assert.AreEqual(20.0, geo.WidthMeters, 1e-9);
        }
    }
}
