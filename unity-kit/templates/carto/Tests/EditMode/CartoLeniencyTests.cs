using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoLeniencyTests
    {
        static PlaniMap Parse(string body) => PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream(
            "<?xml version='1.0' encoding='utf-8'?>" +
            "<PLANI_TYPE3 LIM_EST=\"1\" LIM_NORD=\"1\" LIM_OUEST=\"0\" LIM_SUD=\"0\" NbElements=\"255\">" +
            body + "</PLANI_TYPE3>"));

        [Test]
        public void MissingAttributes_GetTypedDefaults()
        {
            var map = Parse("<ROUTES NbElements=\"1\"><ROUTE>" +
                "<POINTS><POINT X=\"0.5\" Y=\"0.5\" /><POINT X=\"0.6\" Y=\"0.6\" /></POINTS>" +
                "</ROUTE></ROUTES>");
            Assert.AreEqual(1, map.Roads.Count);
            Assert.AreEqual("", map.Roads[0].Info.Name);
            Assert.AreEqual(0, map.Roads[0].Info.Importance);
            Assert.AreEqual(0f, map.Roads[0].Info.Width, 1e-6f);
            Assert.AreEqual(2, map.Roads[0].Points.Length);
        }

        [Test]
        public void NbElementsMismatch_ProducesWarning_NotError()
        {
            var map = Parse("<ROUTES NbElements=\"5\"><ROUTE>" +
                "<POINTS><POINT X=\"0.5\" Y=\"0.5\" /></POINTS></ROUTE></ROUTES>");
            Assert.AreEqual(1, map.Roads.Count);
            Assert.That(map.Warnings, Has.Some.Contains("ROUTES"));
        }

        [Test]
        public void UnknownElements_AreSkipped()
        {
            var map = Parse("<GADGETS NbElements=\"1\"><GADGET FOO=\"1\"><BAR /></GADGET></GADGETS>" +
                "<ROUTES NbElements=\"1\"><ROUTE><POINTS><POINT X=\"0.5\" Y=\"0.5\" /></POINTS></ROUTE></ROUTES>");
            Assert.AreEqual(1, map.Roads.Count);
        }

        [Test]
        public void WrongRoot_Throws()
        {
            Assert.Throws<System.FormatException>(() =>
                PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream("<NOT_A_MAP />")));
        }

        [Test]
        public void DegenerateOuterRing_FeatureSkipped_WithWarning()
        {
            var map = Parse("<BATIMENTS NbElements=\"1\"><BATIMENT NOM=\"b\">" +
                "<CONTOUR><POINTS><POINT X=\"0.1\" Y=\"0.1\" /><POINT X=\"0.2\" Y=\"0.2\" /></POINTS></CONTOUR>" +
                "</BATIMENT></BATIMENTS>");
            Assert.AreEqual(0, map.Buildings.Count);
            Assert.That(map.Warnings, Has.Some.Contains("BATIMENT"));
        }

        [Test]
        public void FrenchDecimalCulture_DoesNotBreakParsing()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("fr-FR");
                var map = Parse("<ROUTES NbElements=\"1\"><ROUTE LARGEUR=\"5.5\">" +
                    "<POINTS><POINT X=\"0.5\" Y=\"0.25\" /></POINTS></ROUTE></ROUTES>");
                Assert.AreEqual(5.5f, map.Roads[0].Info.Width, 1e-6f);
                Assert.AreEqual(0.25, map.Roads[0].Points[0].Lat, 1e-12);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void SelfClosingEmptySections_YieldEmptyLists_NoWarning()
        {
            // Real exports end with e.g. <PLANS_EAU NbElements="0" /> — the dominant shape in file tails.
            var map = Parse("<PLANS_EAU NbElements=\"0\" /><CONSTRUCTIONS NbElements=\"0\" /><BATIMENTS NbElements=\"0\" />");
            Assert.AreEqual(0, map.Water.Count);
            Assert.AreEqual(0, map.Constructions.Count);
            Assert.AreEqual(0, map.Buildings.Count);
            Assert.AreEqual(0, map.Warnings.Count);
        }

        [Test]
        public void MultipleHoles_AllCaptured()
        {
            var map = Parse("<VEGETATIONS NbElements=\"1\"><VEGETATION SURFACE=\"1\">" +
                "<CONTOUR><POINTS><POINT X=\"0\" Y=\"0\" /><POINT X=\"1\" Y=\"0\" /><POINT X=\"1\" Y=\"1\" /><POINT X=\"0\" Y=\"1\" /></POINTS></CONTOUR>" +
                "<ZONES_EXCLUES>" +
                "<ZONE_EXCLUE><CONTOUR><POINTS><POINT X=\"0.1\" Y=\"0.1\" /><POINT X=\"0.2\" Y=\"0.1\" /><POINT X=\"0.15\" Y=\"0.2\" /></POINTS></CONTOUR></ZONE_EXCLUE>" +
                "<ZONE_EXCLUE><CONTOUR><POINTS><POINT X=\"0.5\" Y=\"0.5\" /><POINT X=\"0.6\" Y=\"0.5\" /><POINT X=\"0.55\" Y=\"0.6\" /></POINTS></CONTOUR></ZONE_EXCLUE>" +
                "</ZONES_EXCLUES></VEGETATION></VEGETATIONS>");
            Assert.AreEqual(1, map.Vegetation.Count);
            Assert.AreEqual(2, map.Vegetation[0].Holes.Count);
            Assert.AreEqual(4, map.Vegetation[0].Outer.Length);
        }

        [Test]
        public void DegenerateOuterWithHoles_FeatureSkipped()
        {
            var map = Parse("<PLANS_EAU NbElements=\"1\"><PLAN_EAU>" +
                "<CONTOUR><POINTS><POINT X=\"0\" Y=\"0\" /><POINT X=\"1\" Y=\"1\" /></POINTS></CONTOUR>" +
                "<ZONES_EXCLUES><ZONE_EXCLUE><CONTOUR><POINTS><POINT X=\"0.1\" Y=\"0.1\" /><POINT X=\"0.2\" Y=\"0.1\" /><POINT X=\"0.15\" Y=\"0.2\" /></POINTS></CONTOUR></ZONE_EXCLUE></ZONES_EXCLUES>" +
                "</PLAN_EAU></PLANS_EAU>");
            Assert.AreEqual(0, map.Water.Count);
            Assert.That(map.Warnings, Has.Some.Contains("PLAN_EAU"));
        }

        [Test]
        public void UnparseableCoordinate_WarnsAndDefaultsToZero()
        {
            var map = Parse("<ROUTES NbElements=\"1\"><ROUTE>" +
                "<POINTS><POINT X=\"abc\" Y=\"0.5\" /><POINT X=\"0.6\" Y=\"0.6\" /></POINTS></ROUTE></ROUTES>");
            Assert.AreEqual(1, map.Roads.Count);
            Assert.AreEqual(0.0, map.Roads[0].Points[0].Lon, 1e-12);
            Assert.AreEqual(0.5, map.Roads[0].Points[0].Lat, 1e-12);
            Assert.That(map.Warnings, Has.Some.Contains("unparseable"));
        }
    }
}
