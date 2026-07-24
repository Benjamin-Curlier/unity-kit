---
name: unity-carto-maps
description: Integrate PLANI_TYPE3 "carto" maps (SWORD-family wargame terrain XML + .geo georeferenced rasters) into Unity — the format family, the C# importer template (parse → project → baked CMAP asset → load-time 2D rendering), and the import workflow. Use when a project must load .xml planimetry maps, "carte"/carto GIS data, .geo+gif/tif raster pairs, or modèle-pivot shapefiles.
---

# Carto maps (PLANI_TYPE3 family) in Unity

## What this format family is
Military-simulation terrain: QGIS scripts convert BD TOPO / VMAP1 / OSM / MGCP
shapefiles into "modèle pivot" (MP) layers (DIGEST FACC codes), exported as
**PLANI_TYPE3 XML** (8 feature sections, WGS84 lon/lat, files up to ~102 MB) plus
**.geo + raster** pairs (georeferenced background map). Full schema, leniency
rules, and pipeline inventory: `references/plani-type3.md`. Canonical local
dataset: `C:\Users\bencu\unityProjects\snake-unity-kit\Carto` (internal
IGN-derived training data — never commit, never redistribute; reference by
absolute path).

## The template (in `templates/carto/`)
Three-assembly C# importer — install per the template README:
- `Carto.Core` (engine-free): `PlaniXmlReader` (streaming, lenient),
  `GeoReference`, `LocalProjection` (tangent-plane equirectangular, center =
  LIM midpoints, ~0.16 % edge error at Angers extent), `CartoMapData.Bake` +
  CMAP binary, `PolygonTriangulator` (holes via bridging).
- `Carto.Unity.Runtime`: `CartoMapAsset.Load(TextAsset)`; `CartoMapRenderer`
  builds layer meshes **at load** (children DontSave — a scene with serialized
  meshes for 28 k roads would be ~100 MB of YAML; never do that).
- `Carto.Unity.Editor`: `Unity Kit → Carto → Import PLANI Map...` or
  `CartoImportPipeline.Import(xml, geo, tif)` + `CartoSceneBuilder.BuildScene`.

## Import workflow
1. Source files stay outside `Assets/` — pick them from anywhere on disk.
2. Import writes `Assets/CartoMaps/<name>.cartomap.bytes` (+ `_raster.tif`,
   max texture size 8192). That folder is gitignored (machine-local artifacts).
3. Build the demo scene (tiny — renderer wiring only), then verify per
   `unity-verify`: console clean, EditMode carto tests green, play smoke,
   screenshot showing raster + vector layers registered.

## Conventions and traps
- XY plane, 1 Unity unit = 1 m, origin = map center. Meshes must wind **CW**
  in XY (camera at −Z looking +Z, URP Unlit culls back faces).
- All parsing culture-invariant (French locale breaks naive float parsing).
- Never trust `NbElements`; every attribute optional; accept the `COMMENAIRE`
  typo on PLAN_EAU; `.gif` rasters can't be imported — use the `.tif` sibling.
- Runtime does zero geo-processing: parse+project happen at import; the game
  loads baked bytes only (see unity-geo-maps for the doctrine).

## Future work (recorded, not implemented)
Gameplay-grid bake (per-game design table — see unity-geo-maps), DEM/elevation
fusion (PLANI is planimetric), SWORD DRS export, UPM packaging, raster tiling
beyond 16 k, in-Unity map authoring.

Cross-refs: unity-geo-maps (licensing/OSM/bake doctrine), unity-scene,
unity-verify, unity-csharp.
