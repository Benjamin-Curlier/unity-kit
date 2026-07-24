# Carto map integration — design spec

**Date:** 2026-07-24
**Status:** approved design (brainstorming phase complete)
**Deliverable home:** `unity-kit` plugin (this repo, branch `feat/carto-maps`, targeting **v0.8.0** — v0.7.0 is already in flight on `roadmap/v0.7.0`)
**Demo/verification site:** snake2d Unity project (`C:\Users\bencu\unityProjects\snake-unity-kit`, worktree `carto-map-unity-integration-69e523`)

## 1. Context

The user's `Carto/` folder (`C:\Users\bencu\unityProjects\snake-unity-kit\Carto`, ~17 GB, untracked)
contains a military-simulation terrain pipeline for the city of Angers, France:

- **Source GIS layers** (IGN BD TOPO-derived shapefiles, CRS `WGS_1984_UTM_Zone_30N`):
  roads, industrial/undifferentiated buildings, water, vegetation, railways.
- **QGIS Python scripts** (`ScriptsQGIS/`) converting BD TOPO / VMAP1 / OSM / MGCP sources into a
  "modèle pivot" (MP) shapefile schema tagged with DIGEST FACC codes, then exporting via
  `Convert_MP2R6.py` to the **`PLANI_TYPE3` XML map format**. A `DRS_MGCP2SWORD.py` exporter
  confirms the MASA SWORD wargame family as the surrounding ecosystem.
- **Ready maps:** `angers.xml` (102 MB), `Angers2–5.xml` (29–57 MB), plus a georeferenced raster
  (`angersZUB.tif`/`.gif` 12825×12544 px + `angersZUB.geo` sidecar).

Goal: make this **map type** consumable by Unity, packaged as a specialized part of unity-kit.
This connects to the planned WARNO-like geo RTS (real-world 2D map features as gameplay), but v1
stays game-agnostic.

## 2. Decisions (user-approved 2026-07-24)

1. **Scope: full** — plugin skill + reusable C# template + live import of a real Angers map into a
   Unity scene, verified.
2. **Architecture: C# editor-time importer.** No Python/QGIS dependency on the Unity side.
   API level: **.NET Standard 2.1** (Unity 6000.5 cannot target .NET 10; its scripting runtime is
   the Mono/.NET Standard profile — CoreCLR arrives in a future Unity major). The parsing core is
   written free of `UnityEngine` references so the identical sources also compile under modern .NET
   (e.g. a .NET 10 CLI) if ever needed.
3. **V1 output: data asset + visible 2D map** (no gameplay-grid bake yet — that needs a per-game
   design table and stays a documented follow-up).
4. **Code shipping: template copy-in** under `unity-kit/templates/carto/`; promote to a UPM package
   once the API stabilizes.

## 3. Format reference (to be captured in the skill's `references/`)

### 3.1 `PLANI_TYPE3` XML

Root `<PLANI_TYPE3>`: attributes `LIM_EST`, `LIM_NORD`, `LIM_OUEST`, `LIM_SUD` (WGS84 degrees),
`NbElements` (constant `255` — meaningless, ignore); children `NO`/`NE`/`SE`/`SO` corner elements
(`X` = longitude, `Y` = latitude), then eight feature sections. All coordinates in the file are
WGS84 lon/lat degrees (EPSG:4326).

**Linear sections** — each child element carries attributes + `<POINTS><POINT X Y/>…</POINTS>`:

| Section | Element | Attributes (as written by `Convert_MP2R6.py`) |
|---|---|---|
| `ROUTES` | `ROUTE` | `NOM, LONGUEUR, SITUATION, NBR_VOIES, SEPARATION, REVETEMENT, IMPORTANCE, CATEGORIE, LARGEUR_MAX, LARGEUR, MASSE_MAX, SENS` |
| `PONTS_LINEAIRES` | `PONT_LINEAIRE` | `NOM, LONGUEUR, HAUTEUR_DESSOUS, LARGEUR_MAX, MASSE_MAX` |
| `FLEUVES_LINEAIRES` | `FLEUVE_LINEAIRE` | `TYPE_FLEUVE, SENS_COURANT, VITESSE_COURANT, PROFONDEUR, LARGEUR, LONGUEUR, HAUTEUR, COMMENTAIRE, NOM` |
| `VOIES_FERREES` | `VOIE_FERREE` | `LARGEUR, LONGUEUR, HAUTEUR, COMMENTAIRE, NOM, SITUATION, NBRE_VOIES, TYPE_ECARTEMENT, UTILISATION, TYPE, PHYSIQUE, CLASSEMENT, ECARTEMENT` |

**Zonal sections** — each child element carries attributes + `<CONTOUR><POINTS>…` (outer ring) +
optional `<ZONES_EXCLUES><ZONE_EXCLUE><CONTOUR><POINTS>…` (holes; one `ZONE_EXCLUE` per hole):

| Section | Element | Attributes |
|---|---|---|
| `VEGETATIONS` | `VEGETATION` | `NOM, SURFACE, TYPE_VEGETATION, DENSITE` |
| `PLANS_EAU` | `PLAN_EAU` | `HAUTEUR, COMMENAIRE (sic), NOM, SURFACE` |
| `CONSTRUCTIONS` | `CONSTRUCTION` | `NOM, SURFACE, HAUTEUR, COMMENTAIRE` (holes not emitted by current producer) |
| `BATIMENTS` | `BATIMENT` | `NOM, SURFACE, HAUTEUR, COMMENTAIRE, NB_NIVEAUX` |

**Leniency rules (mandatory parser behavior, evidenced by real files):**

- Every attribute is optional → typed defaults (numeric `0`, string `""`); unknown attributes and
  unknown elements are skipped without error.
- Accept both `COMMENTAIRE` and the producer's `COMMENAIRE` typo on `PLAN_EAU`.
- Never trust `NbElements` (root is a constant; per-section values may drift) — count actual
  elements; expose the declared value for diagnostics only.
- Sections may be empty or absent per export (`angers.xml` has populated `ROUTES`/`VEGETATIONS`
  but `PLANS_EAU`/`CONSTRUCTIONS`/`BATIMENTS` at 0; `Angers5.xml` has populated `BATIMENTS`).
- Polygon rings may or may not repeat the first point as last — normalize (drop duplicate closing
  point).
- Files reach 102 MB — parsing MUST stream (`XmlReader`), never DOM-load.

### 3.2 `.geo` raster georeference

XML `<GeoReference>` sidecar next to a same-named raster (`.gif`/`.tif`):
`PixelMetreX/Y` (meters per pixel), `DimensionImageX/Y` (pixels), `Echelle`
(= meters-per-pixel × 10000, informational), and 8 corner fields
`Longitude{NO,NE,SE,SO}` / `Latitude{NO,NE,SE,SO}`. Produced by `Creer_GeoEtXMLvide.py` /
`Creer_GifEtGeo.py` from the QGIS raster layer extent.

### 3.3 Upstream pipeline (documented for provenance/reproducibility)

`BD TOPO | VMAP1 | OSM | MGCP` shapefiles → QGIS scripts (`Convert_*2MP.py`) → MP layers
(`MP_Zonaux` polygons / `MP_Lineaires` lines / `MP_Points`, fields incl. `NATURE`, FACC code,
`FSC`, `HAUTEUR`, `LARGEUR`, …) → `Convert_MP2R6.py` → `PLANI_TYPE3` XML (+ `.geo`/raster pair).
The skill lists each script's role; unity-kit does not reimplement them.

## 4. C# architecture (template `unity-kit/templates/carto/`)

### 4.1 `Carto.Core` — assembly `Carto.Core.asmdef` (no Unity references)

- `PlaniMap` — parsed model: map bounds + corners (degrees, doubles) and typed lists:
  `Road, Bridge, RiverLine, Railway` (attrs + `GeoPoint[] Points`) and
  `VegetationArea, WaterArea, Construction, Building` (attrs + `GeoRing Outer` +
  `GeoRing[] Holes`). `GeoPoint = (double Lon, double Lat)`.
- `PlaniXmlReader.Read(Stream) → PlaniMap` — streaming, culture-invariant parsing, leniency rules
  above, per-section element counters + collected non-fatal warnings list.
- `GeoReference.Read(Stream)` — `.geo` model (pixel size, dimensions, corners).
- `LocalProjection` — WGS84 → local meters. Tangent-plane equirectangular centered on the map
  center `(lon0, lat0)`:
  `x = (lon − lon0)·cos(lat0)·k`, `y = (lat − lat0)·k`, `k = π/180 · R`, `R = 6378137` (WGS84
  semi-major). Deterministic double math, output truncated to float32. Documented error bound: at
  Angers extent (~19 km N–S, ~±0.084° lat around center) the E–W scale error at the map edge is
  ≈ 0.11 % (≈ 10 m at 9 km) — acceptable for v1 rendering/gameplay; the class is small and
  self-contained so a true transverse-Mercator implementation can replace it later behind the same
  API if survey-grade fidelity is ever needed. Same projection instance is used for map features
  AND raster corners, so layers stay mutually registered regardless of absolute error.
- `CartoMapData` — baked model in local float32 meters: header (source name, version, center
  lon/lat, bounds in meters), per-layer arrays (attributes + packed point/ring data), plus
  `Save(Stream)`/`Load(Stream)` binary serialization: little-endian, magic `CMAP`, `ushort`
  version, counts as `int32`, coordinates as `float32` pairs, strings length-prefixed UTF-8.
  Loadable with zero geo-processing.
- `PolygonTriangulator` — ear clipping; holes merged into the outer ring via the standard
  max-x bridge (Held) before clipping. Input sanitation: drop duplicate/collinear points,
  enforce winding.

### 4.2 Unity assemblies

**`Carto.Unity.Runtime` (`Carto.Unity.Runtime.asmdef`, references `Carto.Core`):**

- `CartoMapAsset.Load(TextAsset) → CartoMapData` — deserializes the baked bytes; game code
  queries typed layers. No geo-processing (parsing/projection happened at import).
- `CartoMapRenderer` MonoBehaviour — holds the baked `TextAsset` + palette; **builds the visual
  meshes at load** (`Awake`, or an editor inspector "Rebuild preview" button creating
  `HideFlags.DontSave` objects). Mesh building from packed local-meter arrays is rendering setup,
  not geo-processing, and takes ~seconds; nothing heavy is ever serialized — the scene file stays
  small (a scene with 28 k roads as serialized meshes would be ~100 MB of YAML; this design
  forbids that). Rendering rules:
  - XY plane, 1 Unity unit = 1 meter, origin = map center;
  - linear layers: width-extruded flat polyline meshes (miter-less segment quads, width from
    `LARGEUR` with per-type minimum), batched into one mesh per layer-category bucket (roads
    bucketed by `IMPORTANCE`);
  - zonal layers: triangulated meshes (holes respected; outer rings normalized CCW, holes CW),
    one combined mesh per layer;
  - flat-color URP-unlit materials per layer (palette constants in code), sorting/Z order:
    raster < water < vegetation < roads < railways < buildings/constructions.

**`Carto.Unity.Editor` (`Carto.Unity.Editor.asmdef`, editor-only):**

- **Menu `Unity Kit → Carto → Import PLANI Map…`** — file pickers for `.xml` (anywhere on disk;
  the source data folder never enters `Assets/`) and optional `.geo` + raster (`.tif` — Unity does
  not import `.gif`; the window says so and offers the `.tif` sibling automatically when present).
  Runs: parse → project → write `Assets/CartoMaps/<name>.cartomap.bytes` + copy raster to
  `Assets/CartoMaps/<name>_raster.tif` (importer settings: texture type Default, sRGB, max
  texture size 8192 — ~2.4 m/px for the Angers raster, ample for an underlay; 16384 documented
  as the ceiling) → optional "Build scene" step. Progress bar + cancellation; import summary
  (element counts, warnings) to the console.
- **`CartoSceneBuilder`** — creates/updates a scene containing only lightweight objects: a root
  `CartoMap_<name>` with `CartoMapRenderer` wired to the baked asset, and a raster underlay
  quad positioned+scaled from the projected `.geo` corners. All heavy visuals come from
  `CartoMapRenderer` at load, per above.

### 4.3 Template layout

```
unity-kit/templates/carto/
  README.md                  (what this is, how sessions copy it, namespaces, asmdef graph)
  Core/                      → copy to Assets/Carto/Core/
  Editor/                    → copy to Assets/Carto/Editor/
  Runtime/                   → copy to Assets/Carto/Runtime/
  Tests/EditMode/            → copy to Assets/Tests/EditMode/Carto/ (+ small synthetic fixtures)
```

No `.meta` files in the template (Unity generates them on copy-in).

## 5. Plugin skill — `unity-kit/skills/unity-carto-maps/`

- `SKILL.md` (concise, per kit conventions): when to use (PLANI/SWORD-family map files, `.geo`
  rasters, "carte"/carto data), what the template provides, import workflow, verify expectations,
  cross-refs: `unity-geo-maps` (licensing/OSM/grid-bake doctrine), `unity-scene`, `unity-verify`.
- `references/plani-type3.md`: the full format reference of §3 (schema tables, leniency rules,
  `.geo` spec, pipeline inventory, known real-file quirks).
- Data policy stated in the skill: source datasets are internal training data (IGN-derived) — keep
  out of git, no redistribution, reference by absolute path; canonical local copy:
  `C:\Users\bencu\unityProjects\snake-unity-kit\Carto`.
- `unity-geo-maps/SKILL.md` gets a one-line cross-ref to `unity-carto-maps`.

## 6. Verification plan (in the snake2d project)

1. Copy template into the worktree's `Assets/` per §4.3.
2. EditMode tests (synthetic fixtures, committed): parser happy path; leniency (missing attrs,
   `COMMENAIRE` typo, wrong `NbElements`, unknown elements, unclosed ring); projection reference
   values (hand-computed) + round-trip; triangulator (convex, concave, with hole); binary
   save/load round-trip equality.
3. Machine-local integration test: import `Angers2.xml` end-to-end; `Assert.Ignore` when the
   Carto folder is absent (CI-safe).
4. Editor import of `Angers2.xml` + `angersZUB.tif`/`.geo` → scene `Assets/Scenes/CartoAngers.unity`.
5. `unity-verify` loop: recompile clean, console clean, EditMode tests green, play-mode smoke on
   the carto scene (exercises the runtime mesh-build path), screenshot showing raster + vector
   layers registered.

The snake game itself is untouched; the carto scene is standalone and committed (it is tiny —
only the renderer wiring). Gitignore additions in the snake repo: `/Carto/` (source dataset) and
`Assets/CartoMaps/` (imported bytes + raster are machine-local derived artifacts; on a checkout
without them the scene simply renders empty until a map is imported).

## 7. Release & repo mechanics

- Plugin work happens in this worktree (`feat/carto-maps` off `main`), NOT on `roadmap/v0.7.0`
  (in flight with uncommitted changes — do not touch).
- Lands as **v0.8.0** after v0.7.0 ships (rebase/merge as needed): plugin.json version bump,
  README + ROADMAP notes, tag, push, reinstall — same release ritual as v0.6.1.
- Commits: spec (this file) → implementation commits → demo-side commits in the snake worktree.

## 8. Acceptance criteria

- [ ] Skill `unity-carto-maps` + `references/plani-type3.md` exist and are self-sufficient
      (a fresh session could implement/extend the importer from them alone).
- [ ] Template compiles in a Unity 6000.5 URP 2D project with zero console errors, .NET Standard
      2.1 API level; `Carto.Core` contains no `UnityEngine` references.
- [ ] All EditMode tests green; integration test imports `Angers2.xml` on this machine.
- [ ] `CartoAngers` scene shows the georeferenced raster with roads/vegetation/water/buildings
      layers visibly registered on top; screenshot captured. Scene file stays lightweight
      (no serialized generated meshes).
- [ ] 17 GB dataset and imported artifacts (`Assets/CartoMaps/`) remain untracked in every repo;
      synthetic fixtures only in git.
- [ ] `feat/carto-maps` branch contains spec + implementation, ready to release as v0.8.0.

## 9. Out of scope (v1) — recorded as future work in the skill

Gameplay-grid bake (movement/cover/LOS — per-game design tables; see `unity-geo-maps` doctrine),
elevation/DEM fusion (PLANI is planimetric), SWORD DRS export, UPM packaging, raster tiling for
>16 k textures, in-Unity map authoring/editing, shapefile (.shp) direct reading.
