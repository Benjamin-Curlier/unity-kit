using Carto.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Carto.Unity.Editor
{
    /// <summary>
    /// Builds the demo scene: ortho camera framing the map, a CartoMapRenderer root
    /// wired to the baked asset, and a georeferenced raster underlay quad. The scene
    /// stays lightweight — all layer meshes are transient (built by the renderer).
    /// </summary>
    public static class CartoSceneBuilder
    {
        public static string BuildScene(CartoImportResult import)
        {
            var data = import.Data;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            var span = data.BoundsMax - data.BoundsMin;
            float halfH = span.Y * 0.55f;
            float halfW = cam.aspect > 0.01f ? span.X * 0.55f / cam.aspect : halfH;
            cam.orthographicSize = Mathf.Max(Mathf.Max(halfH, halfW), 100f); // fit both axes at any view aspect
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(
                (data.BoundsMin.X + data.BoundsMax.X) * 0.5f,
                (data.BoundsMin.Y + data.BoundsMax.Y) * 0.5f, -10f);

            var rootGo = new GameObject("CartoMap_" + data.SourceName);
            var renderer = rootGo.AddComponent<CartoMapRenderer>();
            renderer.mapAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(import.BytesAssetPath);
            renderer.baseMaterial = EnsureUnlitMaterialAsset();

            if (import.RasterAssetPath != null && import.Geo != null)
                AddRasterUnderlay(rootGo.transform, import);

            renderer.Build(); // editor preview — children are DontSave, scene stays small

            string scenePath = "Assets/Scenes/Carto" + data.SourceName + ".unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            return scenePath;
        }

        static Material EnsureUnlitMaterialAsset()
        {
            const string path = CartoImportPipeline.DefaultOutputFolder + "/CartoUnlit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        static void AddRasterUnderlay(Transform parent, CartoImportResult import)
        {
            var geo = import.Geo;
            var proj = new LocalProjection(import.Data.CenterLon, import.Data.CenterLat);
            var nw = proj.Project(geo.CornerNW);
            var ne = proj.Project(geo.CornerNE);
            var se = proj.Project(geo.CornerSE);
            var sw = proj.Project(geo.CornerSW);
            var center = (nw + ne + se + sw) * 0.25f;
            // physical size from pixel metrics; orientation from the projected NW→NE edge
            float angleDeg = Mathf.Atan2(ne.Y - nw.Y, ne.X - nw.X) * Mathf.Rad2Deg;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); // faces −Z → visible to the 2D camera
            quad.name = "RasterUnderlay";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(center.X, center.Y, 0.5f);
            quad.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            quad.transform.localScale = new Vector3((float)geo.WidthMeters, (float)geo.HeightMeters, 1f);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(import.RasterAssetPath);
            string matPath = CartoImportPipeline.DefaultOutputFolder + "/" + import.Data.SourceName + "_RasterMat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = tex; // URP Unlit [MainTexture] = _BaseMap
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets(); // persist the texture ref — machine-local folder, but must survive editor restarts
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
