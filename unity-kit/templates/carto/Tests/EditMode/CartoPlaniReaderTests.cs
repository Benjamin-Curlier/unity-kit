using System.IO;
using System.Text;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoPlaniReaderTests
    {
        // Minimal but structurally faithful PLANI_TYPE3 document.
        public const string SampleXml =
            "<?xml version='1.0' encoding='utf-8'?>" +
            "<PLANI_TYPE3 LIM_EST=\"-0.40\" LIM_NORD=\"47.56\" LIM_OUEST=\"-0.67\" LIM_SUD=\"47.39\" NbElements=\"255\">" +
            "<NO X=\"-0.66\" Y=\"47.565\" /><NE X=\"-0.40\" Y=\"47.56\" /><SE X=\"-0.41\" Y=\"47.39\" /><SO X=\"-0.67\" Y=\"47.395\" />" +
            "<ROUTES NbElements=\"1\">" +
            "<ROUTE NOM=\"A11\" LONGUEUR=\"172.5\" SITUATION=\"8\" NBR_VOIES=\"2\" SEPARATION=\"2\" REVETEMENT=\"2\" IMPORTANCE=\"14\" CATEGORIE=\"2\" LARGEUR_MAX=\"5\" LARGEUR=\"5\" MASSE_MAX=\"120\" SENS=\"1\">" +
            "<POINTS><POINT X=\"-0.50\" Y=\"47.50\" /><POINT X=\"-0.51\" Y=\"47.51\" /><POINT X=\"-0.52\" Y=\"47.515\" /></POINTS>" +
            "</ROUTE></ROUTES>" +
            "<VOIES_FERREES NbElements=\"1\">" +
            "<VOIE_FERREE NOM=\"vf\" LARGEUR=\"10\" NBRE_VOIES=\"1\" ECARTEMENT=\"1.5\">" +
            "<POINTS><POINT X=\"-0.45\" Y=\"47.45\" /><POINT X=\"-0.46\" Y=\"47.46\" /></POINTS>" +
            "</VOIE_FERREE></VOIES_FERREES>" +
            "<VEGETATIONS NbElements=\"1\">" +
            "<VEGETATION NOM=\"\" SURFACE=\"1000\" TYPE_VEGETATION=\"Arbres\" DENSITE=\"51\">" +
            "<CONTOUR><POINTS>" +
            "<POINT X=\"0\" Y=\"0\" /><POINT X=\"0.001\" Y=\"0\" /><POINT X=\"0.001\" Y=\"0.001\" /><POINT X=\"0\" Y=\"0.001\" /><POINT X=\"0\" Y=\"0\" />" +
            "</POINTS></CONTOUR>" +
            "<ZONES_EXCLUES><ZONE_EXCLUE><CONTOUR><POINTS>" +
            "<POINT X=\"0.0004\" Y=\"0.0004\" /><POINT X=\"0.0006\" Y=\"0.0004\" /><POINT X=\"0.0005\" Y=\"0.0006\" />" +
            "</POINTS></CONTOUR></ZONE_EXCLUE></ZONES_EXCLUES>" +
            "</VEGETATION></VEGETATIONS>" +
            "<PLANS_EAU NbElements=\"1\">" +
            "<PLAN_EAU HAUTEUR=\"0\" COMMENAIRE=\"lac\" NOM=\"Maine\" SURFACE=\"500\">" +
            "<CONTOUR><POINTS><POINT X=\"0.01\" Y=\"0.01\" /><POINT X=\"0.02\" Y=\"0.01\" /><POINT X=\"0.015\" Y=\"0.02\" /></POINTS></CONTOUR>" +
            "</PLAN_EAU></PLANS_EAU>" +
            "<BATIMENTS NbElements=\"1\">" +
            "<BATIMENT NOM=\"Bat1\" SURFACE=\"272.27\" HAUTEUR=\"10\" COMMENTAIRE=\"\" NB_NIVEAUX=\"4\">" +
            "<CONTOUR><POINTS><POINT X=\"0.03\" Y=\"0.03\" /><POINT X=\"0.04\" Y=\"0.03\" /><POINT X=\"0.035\" Y=\"0.04\" /></POINTS></CONTOUR>" +
            "</BATIMENT></BATIMENTS>" +
            "</PLANI_TYPE3>";

        public static Stream AsStream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

        [Test]
        public void Read_RootBoundsAndCorners()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(-0.40, map.LimEast, 1e-9);
            Assert.AreEqual(47.56, map.LimNorth, 1e-9);
            Assert.AreEqual(-0.67, map.LimWest, 1e-9);
            Assert.AreEqual(47.39, map.LimSouth, 1e-9);
            Assert.AreEqual(-0.66, map.CornerNW.Lon, 1e-9);
            Assert.AreEqual(47.395, map.CornerSW.Lat, 1e-9);
            Assert.AreEqual(-0.535, map.CenterLon, 1e-9);
            Assert.AreEqual(47.475, map.CenterLat, 1e-9);
        }

        [Test]
        public void Read_Road_AttributesAndPoints()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Roads.Count);
            var road = map.Roads[0];
            Assert.AreEqual("A11", road.Info.Name);
            Assert.AreEqual(172.5f, road.Info.Length, 1e-3f);
            Assert.AreEqual(14, road.Info.Importance);
            Assert.AreEqual(2, road.Info.LaneCount);
            Assert.AreEqual(5f, road.Info.Width, 1e-6f);
            Assert.AreEqual(1, road.Info.Direction);
            Assert.AreEqual(3, road.Points.Length);
            Assert.AreEqual(-0.51, road.Points[1].Lon, 1e-9);
            Assert.AreEqual(47.51, road.Points[1].Lat, 1e-9);
        }

        [Test]
        public void Read_Railway_Parsed()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Railways.Count);
            Assert.AreEqual(1.5f, map.Railways[0].Info.Gauge, 1e-6f);
            Assert.AreEqual(2, map.Railways[0].Points.Length);
        }

        [Test]
        public void Read_Vegetation_OuterRingNormalized_HoleCaptured()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Vegetation.Count);
            var veg = map.Vegetation[0];
            Assert.AreEqual("Arbres", veg.Info.VegetationType);
            Assert.AreEqual(51f, veg.Info.Density, 1e-6f);
            Assert.AreEqual(4, veg.Outer.Length, "closing duplicate point must be dropped");
            Assert.AreEqual(1, veg.Holes.Count);
            Assert.AreEqual(3, veg.Holes[0].Length);
        }

        [Test]
        public void Read_Water_CommenaireTypo_ReadAsComment()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Water.Count);
            Assert.AreEqual("lac", map.Water[0].Info.Comment);
            Assert.AreEqual("Maine", map.Water[0].Info.Name);
        }

        [Test]
        public void Read_Building_Parsed()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Buildings.Count);
            Assert.AreEqual(4, map.Buildings[0].Info.Levels);
            Assert.AreEqual(10f, map.Buildings[0].Info.Height, 1e-6f);
            Assert.AreEqual(3, map.Buildings[0].Outer.Length);
        }

        [Test]
        public void Read_EmptySectionsAbsentSections_YieldEmptyLists()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(0, map.Bridges.Count);     // section absent entirely
            Assert.AreEqual(0, map.RiverLines.Count);  // section absent entirely
            Assert.AreEqual(0, map.Constructions.Count);
        }
    }
}
