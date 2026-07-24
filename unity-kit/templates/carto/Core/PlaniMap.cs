using System.Collections.Generic;

namespace Carto.Core
{
    /// <summary>Linear feature in geographic coordinates.</summary>
    public sealed class GeoLine<T>
    {
        public T Info;
        public GeoPoint[] Points = System.Array.Empty<GeoPoint>();
    }

    /// <summary>Zonal feature in geographic coordinates. Holes come from ZONES_EXCLUES.</summary>
    public sealed class GeoArea<T>
    {
        public T Info;
        public GeoPoint[] Outer = System.Array.Empty<GeoPoint>();
        public List<GeoPoint[]> Holes = new List<GeoPoint[]>();
    }

    /// <summary>Parse-stage model of a PLANI_TYPE3 file. All coordinates WGS84 degrees.</summary>
    public sealed class PlaniMap
    {
        public double LimWest, LimEast, LimSouth, LimNorth;
        public GeoPoint CornerNW, CornerNE, CornerSE, CornerSW;

        public List<GeoLine<RoadInfo>> Roads = new List<GeoLine<RoadInfo>>();
        public List<GeoLine<BridgeInfo>> Bridges = new List<GeoLine<BridgeInfo>>();
        public List<GeoLine<RiverLineInfo>> RiverLines = new List<GeoLine<RiverLineInfo>>();
        public List<GeoLine<RailwayInfo>> Railways = new List<GeoLine<RailwayInfo>>();
        public List<GeoArea<VegetationInfo>> Vegetation = new List<GeoArea<VegetationInfo>>();
        public List<GeoArea<WaterInfo>> Water = new List<GeoArea<WaterInfo>>();
        public List<GeoArea<ConstructionInfo>> Constructions = new List<GeoArea<ConstructionInfo>>();
        public List<GeoArea<BuildingInfo>> Buildings = new List<GeoArea<BuildingInfo>>();

        /// <summary>Non-fatal parse diagnostics (mismatched counts, dropped rings...).</summary>
        public List<string> Warnings = new List<string>();

        public double CenterLon => (LimWest + LimEast) * 0.5;
        public double CenterLat => (LimSouth + LimNorth) * 0.5;
    }
}
