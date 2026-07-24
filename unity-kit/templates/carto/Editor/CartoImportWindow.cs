using System.IO;
using UnityEditor;
using UnityEngine;

namespace Carto.Unity.Editor
{
    public sealed class CartoImportWindow : EditorWindow
    {
        string _xmlPath = "", _geoPath = "", _rasterPath = "";
        CartoImportResult _lastResult;

        [MenuItem("Unity Kit/Carto/Import PLANI Map...")]
        public static void Open() => GetWindow<CartoImportWindow>("Carto Import");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "PLANI_TYPE3 XML → baked .cartomap.bytes (+ optional georeferenced raster underlay).\n" +
                "Source files can live anywhere on disk; only baked artifacts enter Assets/CartoMaps.\n" +
                "Raster must be .tif — Unity cannot import .gif.", MessageType.Info);

            DrawPathField("Map XML", ref _xmlPath, "xml", autoSuggest: true);
            DrawPathField(".geo sidecar (optional)", ref _geoPath, "geo", autoSuggest: false);
            DrawPathField("Raster .tif (optional)", ref _rasterPath, "tif", autoSuggest: false);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_xmlPath)))
                if (GUILayout.Button("Import")) RunImport();

            using (new EditorGUI.DisabledScope(_lastResult == null))
                if (GUILayout.Button("Build Scene"))
                {
                    var scenePath = CartoSceneBuilder.BuildScene(_lastResult);
                    Debug.Log("[Carto] Scene saved: " + scenePath);
                }

            if (_lastResult != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Baked asset", _lastResult.BytesAssetPath);
                EditorGUILayout.LabelField("Bounds (m)",
                    _lastResult.Data.BoundsMin + " → " + _lastResult.Data.BoundsMax);
                EditorGUILayout.LabelField("Warnings", _lastResult.Warnings.Count.ToString());
            }
        }

        void DrawPathField(string label, ref string path, string extension, bool autoSuggest)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(label, path);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    string startDir = string.IsNullOrEmpty(path)
                        ? (string.IsNullOrEmpty(_xmlPath) ? "" : Path.GetDirectoryName(_xmlPath))
                        : Path.GetDirectoryName(path);
                    var picked = EditorUtility.OpenFilePanel(label, startDir, extension);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        path = picked;
                        if (autoSuggest) SuggestSiblings(picked);
                    }
                }
            }
        }

        /// <summary>After picking the XML: default to a sibling raster that has a .geo next to it.</summary>
        void SuggestSiblings(string xmlPath)
        {
            var dir = Path.GetDirectoryName(xmlPath);
            if (dir == null) return;
            foreach (var tif in Directory.GetFiles(dir, "*.tif"))
            {
                var geo = Path.ChangeExtension(tif, ".geo");
                if (!File.Exists(geo)) continue;
                if (string.IsNullOrEmpty(_rasterPath)) _rasterPath = tif;
                if (string.IsNullOrEmpty(_geoPath)) _geoPath = geo;
                break;
            }
        }

        void RunImport()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _lastResult = CartoImportPipeline.Import(_xmlPath, _geoPath, _rasterPath,
                    progress: (msg, t) =>
                    {
                        // cancel takes effect between phases; the parse itself is one opaque call
                        if (EditorUtility.DisplayCancelableProgressBar("Carto Import", msg, t))
                            throw new System.OperationCanceledException("Import canceled");
                    });
                sw.Stop();
                var d = _lastResult.Data;
                Debug.Log(string.Format(
                    "[Carto] Imported {0} in {1:F1}s — roads {2}, bridges {3}, rivers {4}, rail {5}, " +
                    "vegetation {6}, water {7}, constructions {8}, buildings {9}, warnings {10}",
                    d.SourceName, sw.Elapsed.TotalSeconds, d.Roads.Count, d.Bridges.Count,
                    d.RiverLines.Count, d.Railways.Count, d.Vegetation.Count, d.Water.Count,
                    d.Constructions.Count, d.Buildings.Count, _lastResult.Warnings.Count));
                int shown = Mathf.Min(10, _lastResult.Warnings.Count);
                for (int i = 0; i < shown; i++) Debug.LogWarning("[Carto] " + _lastResult.Warnings[i]);
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[Carto] Import canceled");
                _lastResult = null;
            }
            catch (System.Exception ex) when (ex is System.Xml.XmlException || ex is System.FormatException || ex is IOException)
            {
                // corrupt/missing/locked source file → clean error, not a stack trace
                Debug.LogError("[Carto] Import failed: " + ex.Message);
                _lastResult = null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
