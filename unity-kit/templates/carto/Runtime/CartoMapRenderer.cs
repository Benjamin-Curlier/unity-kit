using System.Collections.Generic;
using Carto.Core;
using UnityEngine;
using SysV2 = System.Numerics.Vector2;

namespace Carto.Unity
{
    /// <summary>
    /// Builds all layer meshes at load from a baked .cartomap.bytes TextAsset.
    /// Children carry HideFlags.DontSave — nothing heavy is ever serialized into
    /// the scene (28k roads as serialized meshes would be ~100 MB of YAML).
    /// Editor preview: context menu "Rebuild Preview" on the component.
    /// </summary>
    public sealed class CartoMapRenderer : MonoBehaviour
    {
        [Tooltip("Baked .cartomap.bytes asset produced by Unity Kit > Carto > Import PLANI Map")]
        public TextAsset mapAsset;

        [Tooltip("URP Unlit material; layers tint it via MaterialPropertyBlock. " +
                 "Assign an asset for builds; falls back to Shader.Find at runtime.")]
        public Material baseMaterial;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const string RootName = "__CartoLayers";

        // Layer style table: z order raster(0.5) < water < vegetation < roads < rail < buildings.
        static readonly Color WaterColor = new Color(0.28f, 0.52f, 0.78f);
        static readonly Color VegetationColor = new Color(0.30f, 0.55f, 0.30f);
        static readonly Color RoadMinorColor = new Color(0.35f, 0.35f, 0.35f);
        static readonly Color RoadMajorColor = new Color(0.55f, 0.45f, 0.20f);
        static readonly Color RoadHighwayColor = new Color(0.75f, 0.35f, 0.15f);
        static readonly Color BridgeColor = new Color(0.50f, 0.50f, 0.55f);
        static readonly Color RailColor = new Color(0.20f, 0.20f, 0.25f);
        static readonly Color ConstructionColor = new Color(0.60f, 0.60f, 0.62f);
        static readonly Color BuildingColor = new Color(0.55f, 0.30f, 0.30f);

        void Awake()
        {
            if (Application.isPlaying && mapAsset != null) Build();
        }

        [ContextMenu("Rebuild Preview")]
        public void Build()
        {
            if (mapAsset == null) { Debug.LogWarning("[Carto] No map asset assigned", this); return; }
            BuildFrom(CartoMapAsset.Load(mapAsset));
        }

        public void BuildFrom(CartoMapData data)
        {
            if (data == null) { Debug.LogWarning("[Carto] BuildFrom(null) ignored", this); return; }
            Clear();
            var root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);

            // zonal layers
            AddPolygonLayer(root, "Water", 0.40f, WaterColor, data.Water.ConvertAll(AreaGeom));
            AddPolygonLayer(root, "Vegetation", 0.30f, VegetationColor, data.Vegetation.ConvertAll(AreaGeom));
            AddPolygonLayer(root, "Constructions", 0.12f, ConstructionColor, data.Constructions.ConvertAll(AreaGeom));
            AddPolygonLayer(root, "Buildings", 0.10f, BuildingColor, data.Buildings.ConvertAll(AreaGeom));

            // linear layers
            AddPolylineLayer(root, "RiverLines", 0.41f, WaterColor,
                data.RiverLines.ConvertAll(f => (f.Points, Mathf.Max(f.Info.Width, 4f))));
            AddPolylineLayer(root, "RoadsMinor", 0.20f, RoadMinorColor,
                RoadLines(data, i => i.Importance < 10, 3f));
            AddPolylineLayer(root, "RoadsMajor", 0.19f, RoadMajorColor,
                RoadLines(data, i => i.Importance >= 10 && i.Importance < 14, 4f));
            AddPolylineLayer(root, "RoadsHighway", 0.18f, RoadHighwayColor,
                RoadLines(data, i => i.Importance >= 14, 5f));
            AddPolylineLayer(root, "Bridges", 0.17f, BridgeColor,
                data.Bridges.ConvertAll(f => (f.Points, Mathf.Max(f.Info.WidthMax, 4f))));
            // LARGEUR on VOIE_FERREE is a 10 m corridor default, not track width — fixed 3 m reads better
            AddPolylineLayer(root, "Railways", 0.15f, RailColor,
                data.Railways.ConvertAll(f => (f.Points, 3f)));
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != RootName) continue;
                // destroying a GameObject does NOT destroy the meshes its filters reference —
                // free the previous generation explicitly or every rebuild leaks it
                foreach (var mf in child.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null) DestroyOwned(mf.sharedMesh);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        void OnDestroy()
        {
            Clear();
            if (_fallbackMaterial != null) DestroyOwned(_fallbackMaterial);
        }

        static void DestroyOwned(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        static (SysV2[] outer, SysV2[][] holes) AreaGeom<T>(LocalArea<T> a) => (a.Outer, a.Holes);

        static List<(SysV2[] points, float width)> RoadLines(
            CartoMapData data, System.Predicate<RoadInfo> match, float minWidth)
        {
            var list = new List<(SysV2[] points, float width)>();
            foreach (var r in data.Roads)
                if (match(r.Info))
                    list.Add((r.Points, Mathf.Max(r.Info.Width, minWidth)));
            return list;
        }

        void AddPolygonLayer(GameObject root, string name, float z, Color color,
            List<(SysV2[] outer, SysV2[][] holes)> areas)
        {
            if (areas.Count == 0) return;
            var mesh = CartoMeshBuilder.BuildPolygons(areas, name, out var droppedHoles, out var droppedPolygons);
            if (droppedHoles > 0 || droppedPolygons > 0)
                Debug.LogWarning("[Carto] " + name + ": " + droppedPolygons + " polygon(s) and " +
                                 droppedHoles + " hole(s) could not be triangulated", this);
            AttachMesh(root, name, z, color, mesh);
        }

        void AddPolylineLayer(GameObject root, string name, float z, Color color,
            List<(SysV2[] points, float width)> lines)
        {
            if (lines.Count == 0) return;
            AttachMesh(root, name, z, color, CartoMeshBuilder.BuildPolylines(lines, name));
        }

        void AttachMesh(GameObject root, string name, float z, Color color, Mesh mesh)
        {
            if (mesh.vertexCount == 0)
            {
                // don't orphan the empty native mesh
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                return;
            }
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ResolveMaterial();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, color);
            mr.SetPropertyBlock(mpb);
            // static map layers: free the CPU-side copy (~halves resident mesh memory);
            // vertexCount metadata stays readable for tests/inspection
            mesh.UploadMeshData(markNoLongerReadable: true);
        }

        Material _fallbackMaterial;

        Material ResolveMaterial()
        {
            if (baseMaterial != null) return baseMaterial;
            if (_fallbackMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    Debug.LogError("[Carto] URP Unlit shader not found — assign baseMaterial " +
                                   "(player builds strip unreferenced shaders)", this);
                    return null;
                }
                _fallbackMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            return _fallbackMaterial;
        }
    }
}
