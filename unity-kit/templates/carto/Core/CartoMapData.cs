using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Carto.Core
{
    /// <summary>Linear feature in local meters.</summary>
    public sealed class LocalLine<T>
    {
        public T Info;
        public Vector2[] Points = Array.Empty<Vector2>();
    }

    /// <summary>Zonal feature in local meters.</summary>
    public sealed class LocalArea<T>
    {
        public T Info;
        public Vector2[] Outer = Array.Empty<Vector2>();
        public Vector2[][] Holes = Array.Empty<Vector2[]>();
    }

    /// <summary>
    /// Baked map: all layers in local meters (x east, y north, origin = map center,
    /// 1 unit = 1 m). Binary format "CMAP" v1: little-endian, counts int32, coords
    /// float32 pairs, strings BinaryWriter-style (7-bit length prefix + UTF-8).
    /// Field order per Info struct = declaration order in FeatureInfo.cs — a reorder
    /// requires bumping FormatVersion.
    /// </summary>
    public sealed class CartoMapData
    {
        public const ushort FormatVersion = 1;
        static readonly byte[] Magic = { (byte)'C', (byte)'M', (byte)'A', (byte)'P' };

        public string SourceName = "";
        public double CenterLon, CenterLat;
        public Vector2 BoundsMin, BoundsMax;

        public List<LocalLine<RoadInfo>> Roads = new List<LocalLine<RoadInfo>>();
        public List<LocalLine<BridgeInfo>> Bridges = new List<LocalLine<BridgeInfo>>();
        public List<LocalLine<RiverLineInfo>> RiverLines = new List<LocalLine<RiverLineInfo>>();
        public List<LocalLine<RailwayInfo>> Railways = new List<LocalLine<RailwayInfo>>();
        public List<LocalArea<VegetationInfo>> Vegetation = new List<LocalArea<VegetationInfo>>();
        public List<LocalArea<WaterInfo>> Water = new List<LocalArea<WaterInfo>>();
        public List<LocalArea<ConstructionInfo>> Constructions = new List<LocalArea<ConstructionInfo>>();
        public List<LocalArea<BuildingInfo>> Buildings = new List<LocalArea<BuildingInfo>>();

        // ---- bake ---------------------------------------------------------------

        public static CartoMapData Bake(PlaniMap map, string sourceName)
        {
            var proj = new LocalProjection(map.CenterLon, map.CenterLat);
            var d = new CartoMapData
            {
                SourceName = sourceName,
                CenterLon = map.CenterLon,
                CenterLat = map.CenterLat,
                Roads = BakeLines(map.Roads, proj),
                Bridges = BakeLines(map.Bridges, proj),
                RiverLines = BakeLines(map.RiverLines, proj),
                Railways = BakeLines(map.Railways, proj),
                Vegetation = BakeAreas(map.Vegetation, proj),
                Water = BakeAreas(map.Water, proj),
                Constructions = BakeAreas(map.Constructions, proj),
                Buildings = BakeAreas(map.Buildings, proj)
            };
            d.ComputeBounds();
            return d;
        }

        static Vector2[] ProjectAll(GeoPoint[] pts, LocalProjection proj)
        {
            var v = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) v[i] = proj.Project(pts[i]);
            return v;
        }

        static List<LocalLine<T>> BakeLines<T>(List<GeoLine<T>> src, LocalProjection proj)
        {
            var list = new List<LocalLine<T>>(src.Count);
            foreach (var g in src)
                list.Add(new LocalLine<T> { Info = g.Info, Points = ProjectAll(g.Points, proj) });
            return list;
        }

        static List<LocalArea<T>> BakeAreas<T>(List<GeoArea<T>> src, LocalProjection proj)
        {
            var list = new List<LocalArea<T>>(src.Count);
            foreach (var g in src)
            {
                var holes = new Vector2[g.Holes.Count][];
                for (int i = 0; i < g.Holes.Count; i++) holes[i] = ProjectAll(g.Holes[i], proj);
                list.Add(new LocalArea<T> { Info = g.Info, Outer = ProjectAll(g.Outer, proj), Holes = holes });
            }
            return list;
        }

        void ComputeBounds()
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;
            void Take(Vector2[] pts)
            {
                foreach (var p in pts)
                {
                    any = true;
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
            }
            foreach (var f in Roads) Take(f.Points);
            foreach (var f in Bridges) Take(f.Points);
            foreach (var f in RiverLines) Take(f.Points);
            foreach (var f in Railways) Take(f.Points);
            foreach (var f in Vegetation) { Take(f.Outer); foreach (var h in f.Holes) Take(h); }
            foreach (var f in Water) { Take(f.Outer); foreach (var h in f.Holes) Take(h); }
            foreach (var f in Constructions) { Take(f.Outer); foreach (var h in f.Holes) Take(h); }
            foreach (var f in Buildings) { Take(f.Outer); foreach (var h in f.Holes) Take(h); }
            if (!any) { BoundsMin = Vector2.Zero; BoundsMax = Vector2.Zero; return; }
            BoundsMin = new Vector2(minX, minY);
            BoundsMax = new Vector2(maxX, maxY);
        }

        // ---- binary save --------------------------------------------------------

        public void Save(Stream s)
        {
            using (var w = new BinaryWriter(s, Encoding.UTF8, leaveOpen: true))
            {
                w.Write(Magic);
                w.Write(FormatVersion);
                w.Write(SourceName);
                w.Write(CenterLon);
                w.Write(CenterLat);
                WriteV2(w, BoundsMin);
                WriteV2(w, BoundsMax);
                WriteLines(w, Roads, WriteRoad);
                WriteLines(w, Bridges, WriteBridge);
                WriteLines(w, RiverLines, WriteRiver);
                WriteLines(w, Railways, WriteRailway);
                WriteAreas(w, Vegetation, WriteVegetation);
                WriteAreas(w, Water, WriteWater);
                WriteAreas(w, Constructions, WriteConstruction);
                WriteAreas(w, Buildings, WriteBuilding);
            }
        }

        public static CartoMapData Load(Stream s)
        {
            using (var r = new BinaryReader(s, Encoding.UTF8, leaveOpen: true))
            {
                var m = r.ReadBytes(4);
                if (m.Length != 4 || m[0] != Magic[0] || m[1] != Magic[1] || m[2] != Magic[2] || m[3] != Magic[3])
                    throw new FormatException("Not a CMAP file (bad magic)");
                var version = r.ReadUInt16();
                if (version != FormatVersion)
                    throw new FormatException("Unsupported CMAP version " + version);

                var d = new CartoMapData
                {
                    SourceName = r.ReadString(),
                    CenterLon = r.ReadDouble(),
                    CenterLat = r.ReadDouble(),
                    BoundsMin = ReadV2(r),
                    BoundsMax = ReadV2(r)
                };
                d.Roads = ReadLines(r, ReadRoad);
                d.Bridges = ReadLines(r, ReadBridge);
                d.RiverLines = ReadLines(r, ReadRiver);
                d.Railways = ReadLines(r, ReadRailway);
                d.Vegetation = ReadAreas(r, ReadVegetation);
                d.Water = ReadAreas(r, ReadWater);
                d.Constructions = ReadAreas(r, ReadConstruction);
                d.Buildings = ReadAreas(r, ReadBuilding);
                return d;
            }
        }

        // ---- generic geometry IO ------------------------------------------------

        static void WriteV2(BinaryWriter w, Vector2 v) { w.Write(v.X); w.Write(v.Y); }
        static Vector2 ReadV2(BinaryReader r) => new Vector2(r.ReadSingle(), r.ReadSingle());

        // Counts come from untrusted bytes; a corrupt file must fail as FormatException,
        // never as OutOfMemory/Overflow from pre-allocating a bogus size.
        static int ReadCount(BinaryReader r, int minBytesPerItem)
        {
            int n = r.ReadInt32();
            if (n < 0) throw new FormatException("Corrupt CMAP: negative count " + n);
            var s = r.BaseStream;
            if (s.CanSeek && (long)n * minBytesPerItem > s.Length - s.Position)
                throw new FormatException("Corrupt CMAP: count " + n + " exceeds remaining data");
            return n;
        }

        static void WriteRing(BinaryWriter w, Vector2[] pts)
        {
            w.Write(pts.Length);
            foreach (var p in pts) WriteV2(w, p);
        }

        static Vector2[] ReadRing(BinaryReader r)
        {
            int n = ReadCount(r, 8);
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++) pts[i] = ReadV2(r);
            return pts;
        }

        static void WriteLines<T>(BinaryWriter w, List<LocalLine<T>> list, Action<BinaryWriter, T> wi)
        {
            w.Write(list.Count);
            foreach (var f in list) { wi(w, f.Info); WriteRing(w, f.Points); }
        }

        static List<LocalLine<T>> ReadLines<T>(BinaryReader r, Func<BinaryReader, T> ri)
        {
            int n = ReadCount(r, 1);
            var list = new List<LocalLine<T>>(n);
            for (int i = 0; i < n; i++)
                list.Add(new LocalLine<T> { Info = ri(r), Points = ReadRing(r) });
            return list;
        }

        static void WriteAreas<T>(BinaryWriter w, List<LocalArea<T>> list, Action<BinaryWriter, T> wi)
        {
            w.Write(list.Count);
            foreach (var f in list)
            {
                wi(w, f.Info);
                WriteRing(w, f.Outer);
                w.Write(f.Holes.Length);
                foreach (var h in f.Holes) WriteRing(w, h);
            }
        }

        static List<LocalArea<T>> ReadAreas<T>(BinaryReader r, Func<BinaryReader, T> ri)
        {
            int n = ReadCount(r, 1);
            var list = new List<LocalArea<T>>(n);
            for (int i = 0; i < n; i++)
            {
                var info = ri(r);
                var outer = ReadRing(r);
                var holes = new Vector2[ReadCount(r, 4)][];
                for (int h = 0; h < holes.Length; h++) holes[h] = ReadRing(r);
                list.Add(new LocalArea<T> { Info = info, Outer = outer, Holes = holes });
            }
            return list;
        }

        // ---- per-type info IO (field order = FeatureInfo.cs declaration order) --

        static void WriteRoad(BinaryWriter w, RoadInfo i) { w.Write(i.Name ?? ""); w.Write(i.Length); w.Write(i.Situation); w.Write(i.LaneCount); w.Write(i.Separation); w.Write(i.Pavement); w.Write(i.Importance); w.Write(i.Category); w.Write(i.WidthMax); w.Write(i.Width); w.Write(i.MassMax); w.Write(i.Direction); }
        static RoadInfo ReadRoad(BinaryReader r) => new RoadInfo { Name = r.ReadString(), Length = r.ReadSingle(), Situation = r.ReadInt32(), LaneCount = r.ReadInt32(), Separation = r.ReadInt32(), Pavement = r.ReadInt32(), Importance = r.ReadInt32(), Category = r.ReadInt32(), WidthMax = r.ReadSingle(), Width = r.ReadSingle(), MassMax = r.ReadSingle(), Direction = r.ReadInt32() };

        static void WriteBridge(BinaryWriter w, BridgeInfo i) { w.Write(i.Name ?? ""); w.Write(i.Length); w.Write(i.ClearanceBelow); w.Write(i.WidthMax); w.Write(i.MassMax); }
        static BridgeInfo ReadBridge(BinaryReader r) => new BridgeInfo { Name = r.ReadString(), Length = r.ReadSingle(), ClearanceBelow = r.ReadSingle(), WidthMax = r.ReadSingle(), MassMax = r.ReadSingle() };

        static void WriteRiver(BinaryWriter w, RiverLineInfo i) { w.Write(i.RiverType ?? ""); w.Write(i.Name ?? ""); w.Write(i.Comment ?? ""); w.Write(i.FlowDirection); w.Write(i.FlowSpeed); w.Write(i.Depth); w.Write(i.Width); w.Write(i.Length); w.Write(i.Height); }
        static RiverLineInfo ReadRiver(BinaryReader r) => new RiverLineInfo { RiverType = r.ReadString(), Name = r.ReadString(), Comment = r.ReadString(), FlowDirection = r.ReadInt32(), FlowSpeed = r.ReadSingle(), Depth = r.ReadSingle(), Width = r.ReadSingle(), Length = r.ReadSingle(), Height = r.ReadSingle() };

        static void WriteRailway(BinaryWriter w, RailwayInfo i) { w.Write(i.Name ?? ""); w.Write(i.Comment ?? ""); w.Write(i.Width); w.Write(i.Length); w.Write(i.Height); w.Write(i.Gauge); w.Write(i.Situation); w.Write(i.TrackCount); w.Write(i.GaugeType); w.Write(i.Usage); w.Write(i.Type); w.Write(i.Physical); w.Write(i.Classification); }
        static RailwayInfo ReadRailway(BinaryReader r) => new RailwayInfo { Name = r.ReadString(), Comment = r.ReadString(), Width = r.ReadSingle(), Length = r.ReadSingle(), Height = r.ReadSingle(), Gauge = r.ReadSingle(), Situation = r.ReadInt32(), TrackCount = r.ReadInt32(), GaugeType = r.ReadInt32(), Usage = r.ReadInt32(), Type = r.ReadInt32(), Physical = r.ReadInt32(), Classification = r.ReadInt32() };

        static void WriteVegetation(BinaryWriter w, VegetationInfo i) { w.Write(i.Name ?? ""); w.Write(i.VegetationType ?? ""); w.Write(i.Surface); w.Write(i.Density); }
        static VegetationInfo ReadVegetation(BinaryReader r) => new VegetationInfo { Name = r.ReadString(), VegetationType = r.ReadString(), Surface = r.ReadSingle(), Density = r.ReadSingle() };

        static void WriteWater(BinaryWriter w, WaterInfo i) { w.Write(i.Name ?? ""); w.Write(i.Comment ?? ""); w.Write(i.Height); w.Write(i.Surface); }
        static WaterInfo ReadWater(BinaryReader r) => new WaterInfo { Name = r.ReadString(), Comment = r.ReadString(), Height = r.ReadSingle(), Surface = r.ReadSingle() };

        static void WriteConstruction(BinaryWriter w, ConstructionInfo i) { w.Write(i.Name ?? ""); w.Write(i.Comment ?? ""); w.Write(i.Surface); w.Write(i.Height); }
        static ConstructionInfo ReadConstruction(BinaryReader r) => new ConstructionInfo { Name = r.ReadString(), Comment = r.ReadString(), Surface = r.ReadSingle(), Height = r.ReadSingle() };

        static void WriteBuilding(BinaryWriter w, BuildingInfo i) { w.Write(i.Name ?? ""); w.Write(i.Comment ?? ""); w.Write(i.Surface); w.Write(i.Height); w.Write(i.Levels); }
        static BuildingInfo ReadBuilding(BinaryReader r) => new BuildingInfo { Name = r.ReadString(), Comment = r.ReadString(), Surface = r.ReadSingle(), Height = r.ReadSingle(), Levels = r.ReadInt32() };
    }
}
