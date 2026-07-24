using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Carto.Core
{
    /// <summary>
    /// Streaming PLANI_TYPE3 parser. Files reach 102 MB — never DOM-load.
    /// Leniency rules (evidenced by real exports): all attributes optional with typed
    /// defaults, unknown elements/attributes skipped, COMMENAIRE typo accepted on
    /// PLAN_EAU, NbElements never trusted (actual elements are counted; mismatches
    /// become Warnings), closing duplicate ring points dropped, rings with fewer than
    /// 3 points dropped with a warning.
    /// </summary>
    public static class PlaniXmlReader
    {
        public static PlaniMap Read(Stream stream)
        {
            var map = new PlaniMap();
            var declared = new Dictionary<string, int>();

            var settings = new XmlReaderSettings { IgnoreWhitespace = true };
            using (var r = XmlReader.Create(stream, settings))
            {
                r.MoveToContent();
                if (r.NodeType != XmlNodeType.Element || r.Name != "PLANI_TYPE3")
                    throw new FormatException("Not a PLANI_TYPE3 file (root element: <" + r.Name + ">)");

                map.LimEast = DA(r, "LIM_EST");
                map.LimNorth = DA(r, "LIM_NORD");
                map.LimWest = DA(r, "LIM_OUEST");
                map.LimSouth = DA(r, "LIM_SUD");

                while (r.Read())
                {
                    if (r.NodeType != XmlNodeType.Element) continue;
                    switch (r.Name)
                    {
                        case "NO": map.CornerNW = Corner(r); break;
                        case "NE": map.CornerNE = Corner(r); break;
                        case "SE": map.CornerSE = Corner(r); break;
                        case "SO": map.CornerSW = Corner(r); break;

                        case "ROUTES":
                        case "PONTS_LINEAIRES":
                        case "FLEUVES_LINEAIRES":
                        case "VOIES_FERREES":
                        case "VEGETATIONS":
                        case "PLANS_EAU":
                        case "CONSTRUCTIONS":
                        case "BATIMENTS":
                            declared[r.Name] = IA(r, "NbElements", -1);
                            break;

                        case "ROUTE": map.Roads.Add(ReadLine(r, ReadRoadInfo, map.Warnings, "ROUTE")); break;
                        case "PONT_LINEAIRE": map.Bridges.Add(ReadLine(r, ReadBridgeInfo, map.Warnings, "PONT_LINEAIRE")); break;
                        case "FLEUVE_LINEAIRE": map.RiverLines.Add(ReadLine(r, ReadRiverInfo, map.Warnings, "FLEUVE_LINEAIRE")); break;
                        case "VOIE_FERREE": map.Railways.Add(ReadLine(r, ReadRailwayInfo, map.Warnings, "VOIE_FERREE")); break;
                        case "VEGETATION": AddArea(map.Vegetation, r, ReadVegetationInfo, map.Warnings, "VEGETATION"); break;
                        case "PLAN_EAU": AddArea(map.Water, r, ReadWaterInfo, map.Warnings, "PLAN_EAU"); break;
                        case "CONSTRUCTION": AddArea(map.Constructions, r, ReadConstructionInfo, map.Warnings, "CONSTRUCTION"); break;
                        case "BATIMENT": AddArea(map.Buildings, r, ReadBuildingInfo, map.Warnings, "BATIMENT"); break;
                        // any other element: skipped (leniency)
                    }
                }
            }

            CheckDeclaredCounts(map, declared);
            return map;
        }

        // ---- feature scaffolding -------------------------------------------------

        static GeoPoint Corner(XmlReader r) => new GeoPoint(DA(r, "X"), DA(r, "Y"));

        static GeoLine<T> ReadLine<T>(XmlReader r, Func<XmlReader, T> readInfo, List<string> warnings, string label)
        {
            using (var sub = r.ReadSubtree())
            {
                sub.Read(); // position on the feature element itself
                var line = new GeoLine<T> { Info = readInfo(sub) };
                var pts = new List<GeoPoint>();
                while (sub.Read())
                    if (sub.NodeType == XmlNodeType.Element && sub.Name == "POINT")
                        pts.Add(ReadPoint(sub, warnings, label));
                line.Points = pts.ToArray();
                return line;
            }
        }

        static void AddArea<T>(List<GeoArea<T>> target, XmlReader r, Func<XmlReader, T> readInfo,
                               List<string> warnings, string label)
        {
            using (var sub = r.ReadSubtree())
            {
                sub.Read();
                var area = new GeoArea<T> { Info = readInfo(sub) };
                bool inExcluded = false;
                while (sub.Read())
                {
                    if (sub.NodeType != XmlNodeType.Element) continue;
                    if (sub.Name == "ZONES_EXCLUES") { inExcluded = true; continue; }
                    if (sub.Name != "CONTOUR") continue;

                    var ring = NormalizeRing(ReadRing(sub, warnings, label));
                    if (ring == null) { warnings.Add(label + ": ring with <3 points dropped"); continue; }
                    if (!inExcluded && area.Outer.Length == 0) area.Outer = ring;
                    else area.Holes.Add(ring);
                }
                if (area.Outer.Length == 0)
                {
                    warnings.Add(label + " without a valid outer contour skipped");
                    return;
                }
                target.Add(area);
            }
        }

        static GeoPoint[] ReadRing(XmlReader contourElement, List<string> warnings, string label)
        {
            var pts = new List<GeoPoint>();
            using (var sub = contourElement.ReadSubtree())
            {
                while (sub.Read())
                    if (sub.NodeType == XmlNodeType.Element && sub.Name == "POINT")
                        pts.Add(ReadPoint(sub, warnings, label));
            }
            return pts.ToArray();
        }

        static GeoPoint[] NormalizeRing(GeoPoint[] ring)
        {
            if (ring.Length >= 2 &&
                ring[0].Lon == ring[ring.Length - 1].Lon &&
                ring[0].Lat == ring[ring.Length - 1].Lat)
            {
                var trimmed = new GeoPoint[ring.Length - 1];
                Array.Copy(ring, trimmed, trimmed.Length);
                ring = trimmed;
            }
            return ring.Length < 3 ? null : ring;
        }

        static void CheckDeclaredCounts(PlaniMap map, Dictionary<string, int> declared)
        {
            void Check(string section, int actual)
            {
                if (declared.TryGetValue(section, out var d) && d >= 0 && d != actual)
                    map.Warnings.Add(section + ": declared NbElements=" + d + " but parsed " + actual);
            }
            Check("ROUTES", map.Roads.Count);
            Check("PONTS_LINEAIRES", map.Bridges.Count);
            Check("FLEUVES_LINEAIRES", map.RiverLines.Count);
            Check("VOIES_FERREES", map.Railways.Count);
            Check("VEGETATIONS", map.Vegetation.Count);
            Check("PLANS_EAU", map.Water.Count);
            Check("CONSTRUCTIONS", map.Constructions.Count);
            Check("BATIMENTS", map.Buildings.Count);
        }

        // ---- attribute readers ---------------------------------------------------

        static string S(XmlReader r, string name, string def = "") => r.GetAttribute(name) ?? def;

        static double DA(XmlReader r, string name, double def = 0.0)
        {
            var s = r.GetAttribute(name);
            return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : def;
        }

        static float F(XmlReader r, string name) => (float)DA(r, name);

        // ints may be written with decimals by some producers → parse as double, truncate
        static int IA(XmlReader r, string name, int def = 0) => (int)DA(r, name, def);

        const int MaxWarnings = 100; // cap pathological files; real producers are clean

        static GeoPoint ReadPoint(XmlReader r, List<string> warnings, string label) =>
            new GeoPoint(DCoord(r, "X", warnings, label), DCoord(r, "Y", warnings, label));

        static double DCoord(XmlReader r, string name, List<string> warnings, string label)
        {
            var s = r.GetAttribute(name);
            if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
            if (warnings.Count < MaxWarnings)
                warnings.Add(label + ": POINT with missing/unparseable " + name +
                             (s == null ? "" : "=\"" + s + "\"") + " — 0 used");
            return 0.0;
        }

        // ---- per-type info readers ----------------------------------------------

        static RoadInfo ReadRoadInfo(XmlReader r) => new RoadInfo
        {
            Name = S(r, "NOM"),
            Length = F(r, "LONGUEUR"),
            Situation = IA(r, "SITUATION"),
            LaneCount = IA(r, "NBR_VOIES"),
            Separation = IA(r, "SEPARATION"),
            Pavement = IA(r, "REVETEMENT"),
            Importance = IA(r, "IMPORTANCE"),
            Category = IA(r, "CATEGORIE"),
            WidthMax = F(r, "LARGEUR_MAX"),
            Width = F(r, "LARGEUR"),
            MassMax = F(r, "MASSE_MAX"),
            Direction = IA(r, "SENS")
        };

        static BridgeInfo ReadBridgeInfo(XmlReader r) => new BridgeInfo
        {
            Name = S(r, "NOM"),
            Length = F(r, "LONGUEUR"),
            ClearanceBelow = F(r, "HAUTEUR_DESSOUS"),
            WidthMax = F(r, "LARGEUR_MAX"),
            MassMax = F(r, "MASSE_MAX")
        };

        static RiverLineInfo ReadRiverInfo(XmlReader r) => new RiverLineInfo
        {
            RiverType = S(r, "TYPE_FLEUVE"),
            Name = S(r, "NOM"),
            Comment = S(r, "COMMENTAIRE"),
            FlowDirection = IA(r, "SENS_COURANT"),
            FlowSpeed = F(r, "VITESSE_COURANT"),
            Depth = F(r, "PROFONDEUR"),
            Width = F(r, "LARGEUR"),
            Length = F(r, "LONGUEUR"),
            Height = F(r, "HAUTEUR")
        };

        static RailwayInfo ReadRailwayInfo(XmlReader r) => new RailwayInfo
        {
            Name = S(r, "NOM"),
            Comment = S(r, "COMMENTAIRE"),
            Width = F(r, "LARGEUR"),
            Length = F(r, "LONGUEUR"),
            Height = F(r, "HAUTEUR"),
            Gauge = F(r, "ECARTEMENT"),
            Situation = IA(r, "SITUATION"),
            TrackCount = IA(r, "NBRE_VOIES"),
            GaugeType = IA(r, "TYPE_ECARTEMENT"),
            Usage = IA(r, "UTILISATION"),
            Type = IA(r, "TYPE"),
            Physical = IA(r, "PHYSIQUE"),
            Classification = IA(r, "CLASSEMENT")
        };

        static VegetationInfo ReadVegetationInfo(XmlReader r) => new VegetationInfo
        {
            Name = S(r, "NOM"),
            VegetationType = S(r, "TYPE_VEGETATION"),
            Surface = F(r, "SURFACE"),
            Density = F(r, "DENSITE")
        };

        static WaterInfo ReadWaterInfo(XmlReader r) => new WaterInfo
        {
            Name = S(r, "NOM"),
            // real producer writes the COMMENAIRE typo; accept both spellings
            Comment = S(r, "COMMENTAIRE", S(r, "COMMENAIRE")),
            Height = F(r, "HAUTEUR"),
            Surface = F(r, "SURFACE")
        };

        static ConstructionInfo ReadConstructionInfo(XmlReader r) => new ConstructionInfo
        {
            Name = S(r, "NOM"),
            Comment = S(r, "COMMENTAIRE"),
            Surface = F(r, "SURFACE"),
            Height = F(r, "HAUTEUR")
        };

        static BuildingInfo ReadBuildingInfo(XmlReader r) => new BuildingInfo
        {
            Name = S(r, "NOM"),
            Comment = S(r, "COMMENTAIRE"),
            Surface = F(r, "SURFACE"),
            Height = F(r, "HAUTEUR"),
            Levels = IA(r, "NB_NIVEAUX")
        };
    }
}
