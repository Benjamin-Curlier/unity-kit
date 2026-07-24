using System;
using System.Collections.Generic;
using System.IO;
using Carto.Core;
using UnityEditor;
using UnityEngine;

namespace Carto.Unity.Editor
{
    public sealed class CartoImportResult
    {
        public string BytesAssetPath;
        public string RasterAssetPath; // null when no raster was given
        public CartoMapData Data;
        public GeoReference Geo;       // null when no .geo was given
        public List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// parse (.xml anywhere on disk) → bake → write Assets/CartoMaps/&lt;name&gt;.cartomap.bytes
    /// → copy raster + set import settings. Static and window-free so the import window,
    /// scripts, and editor automation all share one code path.
    /// </summary>
    public static class CartoImportPipeline
    {
        public const string DefaultOutputFolder = "Assets/CartoMaps";

        public static CartoImportResult Import(string xmlPath, string geoPath, string rasterPath,
            string outputFolder = DefaultOutputFolder, Action<string, float> progress = null)
        {
            if (!File.Exists(xmlPath)) throw new FileNotFoundException("PLANI xml not found", xmlPath);
            var result = new CartoImportResult();
            string baseName = Path.GetFileNameWithoutExtension(xmlPath);

            progress?.Invoke("Parsing " + Path.GetFileName(xmlPath), 0.05f);
            PlaniMap map;
            using (var fs = File.OpenRead(xmlPath)) map = PlaniXmlReader.Read(fs);
            result.Warnings.AddRange(map.Warnings);

            progress?.Invoke("Baking to local meters", 0.55f);
            result.Data = CartoMapData.Bake(map, baseName);

            if (!string.IsNullOrEmpty(geoPath) && File.Exists(geoPath))
                using (var gs = File.OpenRead(geoPath)) result.Geo = GeoReference.Read(gs);

            progress?.Invoke("Writing baked asset", 0.75f);
            if (!AssetDatabase.IsValidFolder(outputFolder))
                Directory.CreateDirectory(outputFolder);
            string bytesPath = outputFolder + "/" + baseName + ".cartomap.bytes";
            using (var fs = File.Create(bytesPath)) result.Data.Save(fs);
            result.BytesAssetPath = bytesPath;

            if (!string.IsNullOrEmpty(rasterPath) && File.Exists(rasterPath))
            {
                progress?.Invoke("Copying raster", 0.85f);
                string rasterAsset = outputFolder + "/" + baseName + "_raster" + Path.GetExtension(rasterPath);
                File.Copy(rasterPath, rasterAsset, overwrite: true);
                result.RasterAssetPath = rasterAsset;
            }

            progress?.Invoke("Importing assets", 0.92f);
            AssetDatabase.Refresh();

            if (result.RasterAssetPath != null)
            {
                var ti = AssetImporter.GetAtPath(result.RasterAssetPath) as TextureImporter;
                if (ti != null && ti.maxTextureSize < 8192)
                {
                    // ~2.4 m/px for the Angers raster — ample for an underlay; 16384 is the ceiling
                    ti.maxTextureSize = 8192;
                    ti.SaveAndReimport();
                }
            }
            return result;
        }
    }
}
