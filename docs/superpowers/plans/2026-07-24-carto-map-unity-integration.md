# Carto Map Unity Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the PLANI_TYPE3/`.geo` map format family loadable by Unity — engine-free C# parser + baked binary asset + load-time 2D rendering — shipped as a unity-kit skill + template and proven by importing a real Angers map.

**Architecture:** Three assemblies copied into a Unity project: `Carto.Core` (no UnityEngine — streaming XML parser, tangent-plane projection, baked binary model, triangulator), `Carto.Unity.Runtime` (TextAsset loader + `CartoMapRenderer` that builds meshes at load; nothing heavy serialized), `Carto.Unity.Editor` (import pipeline + window + scene builder). Spec: `docs/superpowers/specs/2026-07-24-carto-map-unity-integration-design.md` (same repo).

**Tech Stack:** Unity 6000.5.4f1, URP 2D, .NET Standard 2.1, `System.Xml` (XmlReader), `System.Numerics.Vector2` in Core, NUnit EditMode tests via MCP for Unity `run_tests`.

---

## Conventions (read once, applies to every task)

- **SNAKE** = `C:\Users\bencu\unityProjects\snake-unity-kit\.claude\worktrees\carto-map-unity-integration-69e523` (branch `claude/carto-map-unity-integration-69e523`). All C#/tests/demo work happens here.
- **PLUGIN** = `C:\Users\bencu\claude-plugins-carto` (branch `feat/carto-maps`). Skill, template sync, release prep happen here.
- **DATA** = `C:\Users\bencu\unityProjects\snake-unity-kit\Carto` (17 GB, untracked, main checkout — NOT in the worktree). Never copy it into any repo.
- **After every script/asmdef change:** poll MCP resource `mcpforunity://editor/state` until `data.is_compiling` is false, then `mcp__UnityMCP__read_console` (types errors) — must be empty before proceeding. This is the project's verify ritual; it replaces "compile" in the steps below.
- **Run EditMode tests:** `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_filter: "Snake2D.Tests.Carto"` (all carto tests) or the class name given in the step. If the `testing` tool group is inactive, activate it first via `mcp__UnityMCP__manage_tools`.
- **Commits:** in SNAKE for code/tests/scene; in PLUGIN for skill/template/release. Message format as shown per task; append the standard `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` trailer to every commit.
- **Culture:** every string↔number conversion uses `CultureInfo.InvariantCulture`. French Windows locale would otherwise parse `"47.5"` as 475.
- Namespaces: `Carto.Core`, `Carto.Unity`. Test namespace: `Snake2D.Tests.Carto`.
- In `Carto.Core` and its tests, `Vector2` means `System.Numerics.Vector2`. Runtime/editor files that need both alias it: `using SysV2 = System.Numerics.Vector2;`.

## File Map

**SNAKE (create):**

| File | Responsibility |
|---|---|
| `Assets/Carto/Core/Carto.Core.asmdef` | Engine-free assembly (`noEngineReferences: true`) |
| `Assets/Carto/Core/Geometry.cs` | `GeoPoint` (double lon/lat) |
| `Assets/Carto/Core/FeatureInfo.cs` | 8 attribute structs shared by parse + baked models |
| `Assets/Carto/Core/PlaniMap.cs` | Parse-stage model: `GeoLine<T>`/`GeoArea<T>` + `PlaniMap` |
| `Assets/Carto/Core/LocalProjection.cs` | WGS84 → local meters (tangent-plane equirectangular) |
| `Assets/Carto/Core/GeoReference.cs` | `.geo` sidecar parser |
| `Assets/Carto/Core/PlaniXmlReader.cs` | Streaming PLANI_TYPE3 parser (leniency rules) |
| `Assets/Carto/Core/CartoMapData.cs` | Baked model (float meters) + `Bake` + binary `Save`/`Load` |
| `Assets/Carto/Core/PolygonTriangulator.cs` | Ear clipping with hole bridging |
| `Assets/Carto/Runtime/Carto.Unity.Runtime.asmdef` | Runtime assembly (refs Carto.Core) |
| `Assets/Carto/Runtime/CartoMapAsset.cs` | `TextAsset` → `CartoMapData` |
| `Assets/Carto/Runtime/CartoMeshBuilder.cs` | Polyline/polygon `Mesh` construction (static, testable) |
| `Assets/Carto/Runtime/CartoMapRenderer.cs` | MonoBehaviour: builds layer objects at load / context menu |
| `Assets/Carto/Editor/Carto.Unity.Editor.asmdef` | Editor assembly |
| `Assets/Carto/Editor/CartoImportPipeline.cs` | parse→bake→write bytes→copy raster (static, testable) |
| `Assets/Carto/Editor/CartoImportWindow.cs` | Menu `Unity Kit/Carto/Import PLANI Map...` |
| `Assets/Carto/Editor/CartoSceneBuilder.cs` | Demo scene: renderer root + raster underlay + camera |
| `Assets/Tests/EditMode/Carto/*.cs` | All tests (files named per task) |

**SNAKE (modify):** `.gitignore` (data exclusions), `Assets/Tests/EditMode/Snake2D.EditMode.Tests.asmdef` (add Carto refs).

**PLUGIN (create/modify):** `unity-kit/templates/carto/**` (synced sources + README), `unity-kit/skills/unity-carto-maps/{SKILL.md,references/plani-type3.md}`, `unity-kit/skills/unity-geo-maps/SKILL.md` (cross-ref), `unity-kit/.claude-plugin/plugin.json` (0.8.0), `unity-kit/README.md`, `ROADMAP.md` (one-liners).

---

### Task 0: Editor up + data hygiene

**Files:** Modify: `.gitignore` (SNAKE root)

- [ ] **Step 0.1: Launch the Unity editor for SNAKE.** Use the `unity-kit:unity-launch` skill with project path = SNAKE. First open generates `Library/` (slow, several minutes). Wait until `mcpforunity://editor/state` answers and `data.advice.ready_for_tools` is true.

- [ ] **Step 0.2: Activate the `testing` tool group** via `mcp__UnityMCP__manage_tools` (list groups, enable `testing`) so `run_tests` is callable later.

- [ ] **Step 0.3: Append data exclusions to `.gitignore`** (SNAKE root), after the `# Crash reports` block:

```gitignore

# Carto GIS source data & imported artifacts (machine-local, never commit)
/Carto/
/Assets/CartoMaps/
/Assets/CartoMaps.meta
```

- [ ] **Step 0.4: Commit** (SNAKE):

```bash
git add .gitignore
git commit -m "chore: ignore Carto source data and imported map artifacts"
```

---

### Task 1: Carto.Core skeleton — asmdef + models

**Files:**
- Create: `Assets/Carto/Core/Carto.Core.asmdef`, `Assets/Carto/Core/Geometry.cs`, `Assets/Carto/Core/FeatureInfo.cs`, `Assets/Carto/Core/PlaniMap.cs`

No behavior yet → no test; the verify ritual (clean compile) is the gate. Model code is exercised by every later test.

- [ ] **Step 1.1: Create `Assets/Carto/Core/Carto.Core.asmdef`:**

```json
{
    "name": "Carto.Core",
    "rootNamespace": "Carto.Core",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

- [ ] **Step 1.2: Create `Assets/Carto/Core/Geometry.cs`:**

```csharp
namespace Carto.Core
{
    /// <summary>Geographic coordinate, WGS84 decimal degrees.</summary>
    public readonly struct GeoPoint
    {
        public readonly double Lon;
        public readonly double Lat;

        public GeoPoint(double lon, double lat)
        {
            Lon = lon;
            Lat = lat;
        }

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:F7}, {1:F7})", Lon, Lat);
    }
}
```

- [ ] **Step 1.3: Create `Assets/Carto/Core/FeatureInfo.cs`** — attribute structs shared by the parse-stage and baked models. Field order here is also the binary serialization order (Task 6).

```csharp
namespace Carto.Core
{
    // Attribute names map 1:1 to PLANI_TYPE3 XML attributes (see the format reference
    // in the unity-carto-maps skill). All fields optional in the XML → defaults 0/"".

    public struct RoadInfo
    {
        public string Name;        // NOM
        public float Length;       // LONGUEUR (meters, as written by producer)
        public int Situation;      // SITUATION
        public int LaneCount;      // NBR_VOIES
        public int Separation;     // SEPARATION
        public int Pavement;       // REVETEMENT (pavement/surfacing type code — NOT an area)
        public int Importance;     // IMPORTANCE
        public int Category;       // CATEGORIE
        public float WidthMax;     // LARGEUR_MAX
        public float Width;        // LARGEUR
        public float MassMax;      // MASSE_MAX
        public int Direction;      // SENS
    }

    public struct BridgeInfo
    {
        public string Name;            // NOM
        public float Length;           // LONGUEUR
        public float ClearanceBelow;   // HAUTEUR_DESSOUS
        public float WidthMax;         // LARGEUR_MAX
        public float MassMax;          // MASSE_MAX
    }

    public struct RiverLineInfo
    {
        public string RiverType;   // TYPE_FLEUVE
        public string Name;        // NOM
        public string Comment;     // COMMENTAIRE
        public int FlowDirection;  // SENS_COURANT
        public float FlowSpeed;    // VITESSE_COURANT
        public float Depth;        // PROFONDEUR
        public float Width;        // LARGEUR
        public float Length;       // LONGUEUR
        public float Height;       // HAUTEUR
    }

    public struct RailwayInfo
    {
        public string Name;        // NOM
        public string Comment;     // COMMENTAIRE
        public float Width;        // LARGEUR
        public float Length;       // LONGUEUR
        public float Height;       // HAUTEUR
        public float Gauge;        // ECARTEMENT
        public int Situation;      // SITUATION
        public int TrackCount;     // NBRE_VOIES
        public int GaugeType;      // TYPE_ECARTEMENT
        public int Usage;          // UTILISATION
        public int Type;           // TYPE
        public int Physical;       // PHYSIQUE
        public int Classification; // CLASSEMENT
    }

    public struct VegetationInfo
    {
        public string Name;            // NOM
        public string VegetationType;  // TYPE_VEGETATION
        public float Surface;          // SURFACE (m²)
        public float Density;          // DENSITE
    }

    public struct WaterInfo
    {
        public string Name;    // NOM
        public string Comment; // COMMENTAIRE or the producer's COMMENAIRE typo
        public float Height;   // HAUTEUR
        public float Surface;  // SURFACE
    }

    public struct ConstructionInfo
    {
        public string Name;    // NOM
        public string Comment; // COMMENTAIRE
        public float Surface;  // SURFACE
        public float Height;   // HAUTEUR
    }

    public struct BuildingInfo
    {
        public string Name;    // NOM
        public string Comment; // COMMENTAIRE
        public float Surface;  // SURFACE
        public float Height;   // HAUTEUR
        public int Levels;     // NB_NIVEAUX
    }
}
```

- [ ] **Step 1.4: Create `Assets/Carto/Core/PlaniMap.cs`:**

```csharp
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
```

- [ ] **Step 1.5: Verify ritual** (compile-wait + empty console).

- [ ] **Step 1.6: Commit** (SNAKE):

```bash
git add Assets/Carto
git commit -m "feat(carto): Carto.Core assembly skeleton — GeoPoint, feature info structs, PlaniMap model"
```

---

### Task 2: LocalProjection (TDD)

**Files:**
- Create: `Assets/Carto/Core/LocalProjection.cs`
- Create: `Assets/Tests/EditMode/Carto/CartoProjectionTests.cs`
- Modify: `Assets/Tests/EditMode/Snake2D.EditMode.Tests.asmdef`

- [ ] **Step 2.1: Add the Carto.Core reference to the EditMode test asmdef.** Replace the `references` array in `Assets/Tests/EditMode/Snake2D.EditMode.Tests.asmdef` (keep every other field as-is). Only reference assemblies that already exist — an unresolved reference logs a warning and pollutes the console-clean ritual (`Carto.Unity.Runtime` gets added in Task 8):

```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Snake2D",
        "Carto.Core"
    ],
```

- [ ] **Step 2.2: Write the failing test** — `Assets/Tests/EditMode/Carto/CartoProjectionTests.cs`:

```csharp
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoProjectionTests
    {
        // 1 degree of latitude on the WGS84 sphere model: PI/180 * 6378137 m.
        const float MetersPerDegree = 111319.49f;

        [Test]
        public void Project_OneDegreeLat_FromEquatorCenter_IsMetersPerDegree()
        {
            var proj = new LocalProjection(0.0, 0.0);
            var v = proj.Project(new GeoPoint(0.0, 1.0));
            Assert.AreEqual(0f, v.X, 0.01f);
            Assert.AreEqual(MetersPerDegree, v.Y, 0.5f);
        }

        [Test]
        public void Project_OneDegreeLon_At60North_IsHalved_ByCosLat()
        {
            var proj = new LocalProjection(0.0, 60.0);
            var v = proj.Project(new GeoPoint(1.0, 60.0));
            Assert.AreEqual(MetersPerDegree * 0.5f, v.X, 0.5f); // cos(60°) = 0.5
            Assert.AreEqual(0f, v.Y, 0.01f);
        }

        [Test]
        public void Project_CenterMapsToOrigin()
        {
            var proj = new LocalProjection(-0.55, 47.47); // ~Angers
            var v = proj.Project(new GeoPoint(-0.55, 47.47));
            Assert.AreEqual(0f, v.X, 1e-3f);
            Assert.AreEqual(0f, v.Y, 1e-3f);
        }

        [Test]
        public void Unproject_RoundTrips_WithinCentimeters()
        {
            var proj = new LocalProjection(-0.55, 47.47);
            var p0 = new GeoPoint(-0.404, 47.559); // Angers NE-ish corner
            var v = proj.Project(p0);
            var p1 = proj.Unproject(v);
            Assert.AreEqual(p0.Lon, p1.Lon, 1e-6); // ~0.1 m in lon at this latitude
            Assert.AreEqual(p0.Lat, p1.Lat, 1e-6);
        }
    }
}
```

- [ ] **Step 2.3: Run tests, expect failure.** Verify ritual first (test file references `LocalProjection` which doesn't exist → expect **compile errors** in the console naming `LocalProjection`). That's this cycle's "failing test".

- [ ] **Step 2.4: Implement `Assets/Carto/Core/LocalProjection.cs`:**

```csharp
using System;
using System.Numerics;

namespace Carto.Core
{
    /// <summary>
    /// Tangent-plane equirectangular projection centered on (CenterLon, CenterLat).
    /// x = east meters, y = north meters. Double math, rounded to float32 output.
    /// E–W scale/shape distortion grows with |lat − CenterLat| (rel. error ≈ tan(lat0)·Δlat):
    /// at the Angers extent (±0.084° of latitude) that is ≈0.16 % (~15 m at the far corners).
    /// Absolute agreement with UTM ground truth is looser (spherical model) — but features
    /// and raster corners share the same center, so relative registration stays exact.
    /// Deterministic only per single offline bake: Math.Cos is not bit-identical across
    /// platforms — never run this per-client in a lockstep sim. Swap this class behind the
    /// same API if survey-grade fidelity is ever needed.
    /// </summary>
    public sealed class LocalProjection
    {
        public const double EarthRadius = 6378137.0; // WGS84 semi-major axis

        public double CenterLon { get; }
        public double CenterLat { get; }

        readonly double _mPerDegLat;
        readonly double _mPerDegLon;

        public LocalProjection(double centerLon, double centerLat)
        {
            CenterLon = centerLon;
            CenterLat = centerLat;
            _mPerDegLat = Math.PI / 180.0 * EarthRadius;
            _mPerDegLon = _mPerDegLat * Math.Cos(centerLat * Math.PI / 180.0);
        }

        public Vector2 Project(GeoPoint p) => new Vector2(
            (float)((p.Lon - CenterLon) * _mPerDegLon),
            (float)((p.Lat - CenterLat) * _mPerDegLat));

        public GeoPoint Unproject(Vector2 v) => new GeoPoint(
            CenterLon + v.X / _mPerDegLon,
            CenterLat + v.Y / _mPerDegLat);
    }
}
```

- [ ] **Step 2.5: Run tests, expect pass.** Verify ritual, then `run_tests` EditMode with `test_filter: "Snake2D.Tests.Carto.CartoProjectionTests"`. Expected: 4/4 pass.

- [ ] **Step 2.6: Commit** (SNAKE):

```bash
git add Assets/Carto/Core/LocalProjection.cs Assets/Tests/EditMode/Carto Assets/Tests/EditMode/Snake2D.EditMode.Tests.asmdef
git commit -m "feat(carto): tangent-plane local projection with reference-value tests"
```

---

### Task 3: GeoReference (.geo) parser (TDD)

**Files:**
- Create: `Assets/Carto/Core/GeoReference.cs`
- Create: `Assets/Tests/EditMode/Carto/CartoGeoReferenceTests.cs`

- [ ] **Step 3.1: Write the failing test** — `Assets/Tests/EditMode/Carto/CartoGeoReferenceTests.cs`:

```csharp
using System.IO;
using System.Text;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoGeoReferenceTests
    {
        const string SampleGeo =
            "<?xml version='1.0' encoding='utf-8'?>" +
            "<GeoReference><PixelMetreX>1.5057104143025681</PixelMetreX>" +
            "<PixelMetreY>1.5057104143025402</PixelMetreY>" +
            "<DimensionImageX>12825</DimensionImageX><DimensionImageY>12544</DimensionImageY>" +
            "<Echelle>15057</Echelle>" +
            "<LongitudeNO>-0.6603954426382074</LongitudeNO><LatitudeNO>47.56499839262511</LatitudeNO>" +
            "<LongitudeNE>-0.40392536331497525</LongitudeNE><LatitudeNE>47.55947681859172</LatitudeNE>" +
            "<LongitudeSE>-0.41227943000535916</LongitudeSE><LatitudeSE>47.389699733997716</LatitudeSE>" +
            "<LongitudeSO>-0.6679261259036828</LongitudeSO><LatitudeSO>47.39518877466679</LatitudeSO>" +
            "</GeoReference>";

        static Stream AsStream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

        [Test]
        public void Read_ParsesAllFields()
        {
            var geo = GeoReference.Read(AsStream(SampleGeo));
            Assert.AreEqual(1.5057104143025681, geo.PixelMetreX, 1e-12);
            Assert.AreEqual(1.5057104143025402, geo.PixelMetreY, 1e-12);
            Assert.AreEqual(12825, geo.DimensionImageX);
            Assert.AreEqual(12544, geo.DimensionImageY);
            Assert.AreEqual(15057, geo.Echelle);
            Assert.AreEqual(-0.6603954426382074, geo.CornerNW.Lon, 1e-12);
            Assert.AreEqual(47.56499839262511, geo.CornerNW.Lat, 1e-12);
            Assert.AreEqual(-0.40392536331497525, geo.CornerNE.Lon, 1e-12);
            Assert.AreEqual(47.389699733997716, geo.CornerSE.Lat, 1e-12);
            Assert.AreEqual(-0.6679261259036828, geo.CornerSW.Lon, 1e-12);
            Assert.AreEqual(47.55947681859172, geo.CornerNE.Lat, 1e-12);
            Assert.AreEqual(-0.41227943000535916, geo.CornerSE.Lon, 1e-12);
            Assert.AreEqual(47.39518877466679, geo.CornerSW.Lat, 1e-12);
            Assert.AreEqual(1.5057104143025681 * 12825, geo.WidthMeters, 1e-6);
            Assert.AreEqual(1.5057104143025402 * 12544, geo.HeightMeters, 1e-6);
        }

        [Test]
        public void Read_MissingFields_DefaultToZero_NoThrow()
        {
            var geo = GeoReference.Read(AsStream("<GeoReference><PixelMetreX>2.0</PixelMetreX></GeoReference>"));
            Assert.AreEqual(2.0, geo.PixelMetreX, 1e-12);
            Assert.AreEqual(0, geo.DimensionImageX);
            Assert.AreEqual(0.0, geo.CornerNW.Lon, 1e-12);
        }

        [Test]
        public void Read_UnknownNestedElementsAndComments_AreSkipped()
        {
            var geo = GeoReference.Read(AsStream(
                "<GeoReference><!-- comment --><Foo><Bar/></Foo>" +
                "<PixelMetreX>2.0</PixelMetreX><!-- c2 --><DimensionImageX>10</DimensionImageX>" +
                "</GeoReference>"));
            Assert.AreEqual(2.0, geo.PixelMetreX, 1e-12);
            Assert.AreEqual(10, geo.DimensionImageX);
            Assert.AreEqual(20.0, geo.WidthMeters, 1e-9);
        }
    }
}
```

- [ ] **Step 3.2: Run, expect compile errors naming `GeoReference`** (verify ritual).

- [ ] **Step 3.3: Implement `Assets/Carto/Core/GeoReference.cs`:**

```csharp
using System.Globalization;
using System.IO;
using System.Xml;

namespace Carto.Core
{
    /// <summary>
    /// Georeference sidecar (.geo) for a same-named raster (.tif/.gif):
    /// pixel size in meters, image dimensions, and the four corner coordinates.
    /// </summary>
    public sealed class GeoReference
    {
        public double PixelMetreX, PixelMetreY;
        public int DimensionImageX, DimensionImageY;
        public int Echelle; // producer writes meters-per-pixel * 10000; informational
        public GeoPoint CornerNW, CornerNE, CornerSE, CornerSW;

        public double WidthMeters => PixelMetreX * DimensionImageX;
        public double HeightMeters => PixelMetreY * DimensionImageY;

        public static GeoReference Read(Stream stream)
        {
            var g = new GeoReference();
            double lonNO = 0, latNO = 0, lonNE = 0, latNE = 0;
            double lonSE = 0, latSE = 0, lonSO = 0, latSO = 0;

            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };
            using (var r = XmlReader.Create(stream, settings))
            {
                r.MoveToContent();
                if (r.IsEmptyElement) { return g; }
                r.ReadStartElement(); // enter <GeoReference>
                while (r.NodeType == XmlNodeType.Element)
                {
                    string name = r.Name;
                    if (!IsKnownField(name)) { r.Skip(); continue; } // unknown (incl. nested) → next sibling
                    string text = r.ReadElementContentAsString(); // consumes element, lands on next sibling
                    switch (name)
                    {
                        case "PixelMetreX": g.PixelMetreX = D(text); break;
                        case "PixelMetreY": g.PixelMetreY = D(text); break;
                        case "DimensionImageX": g.DimensionImageX = (int)D(text); break;
                        case "DimensionImageY": g.DimensionImageY = (int)D(text); break;
                        case "Echelle": g.Echelle = (int)D(text); break;
                        case "LongitudeNO": lonNO = D(text); break;
                        case "LatitudeNO": latNO = D(text); break;
                        case "LongitudeNE": lonNE = D(text); break;
                        case "LatitudeNE": latNE = D(text); break;
                        case "LongitudeSE": lonSE = D(text); break;
                        case "LatitudeSE": latSE = D(text); break;
                        case "LongitudeSO": lonSO = D(text); break;
                        case "LatitudeSO": latSO = D(text); break;
                    }
                }
            }

            g.CornerNW = new GeoPoint(lonNO, latNO);
            g.CornerNE = new GeoPoint(lonNE, latNE);
            g.CornerSE = new GeoPoint(lonSE, latSE);
            g.CornerSW = new GeoPoint(lonSO, latSO);
            return g;
        }

        static bool IsKnownField(string name)
        {
            switch (name)
            {
                case "PixelMetreX": case "PixelMetreY":
                case "DimensionImageX": case "DimensionImageY":
                case "Echelle":
                case "LongitudeNO": case "LatitudeNO":
                case "LongitudeNE": case "LatitudeNE":
                case "LongitudeSE": case "LatitudeSE":
                case "LongitudeSO": case "LatitudeSO":
                    return true;
                default:
                    return false;
            }
        }

        static double D(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;
    }
}
```

- [ ] **Step 3.4: Run tests, expect pass** — `test_filter: "Snake2D.Tests.Carto.CartoGeoReferenceTests"`, 2/2 pass.

- [ ] **Step 3.5: Commit** (SNAKE):

```bash
git add Assets/Carto/Core/GeoReference.cs Assets/Tests/EditMode/Carto/CartoGeoReferenceTests.cs
git commit -m "feat(carto): .geo raster georeference parser"
```

---

### Task 4: PlaniXmlReader — happy path (TDD)

**Files:**
- Create: `Assets/Carto/Core/PlaniXmlReader.cs`
- Create: `Assets/Tests/EditMode/Carto/CartoPlaniReaderTests.cs`

- [ ] **Step 4.1: Write the failing test** — `Assets/Tests/EditMode/Carto/CartoPlaniReaderTests.cs`:

```csharp
using System.IO;
using System.Text;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoPlaniReaderTests
    {
        // Minimal but structurally faithful PLANI_TYPE3 document.
        public const string SampleXml =
            "<?xml version='1.0' encoding='utf-8'?>" +
            "<PLANI_TYPE3 LIM_EST=\"-0.40\" LIM_NORD=\"47.56\" LIM_OUEST=\"-0.67\" LIM_SUD=\"47.39\" NbElements=\"255\">" +
            "<NO X=\"-0.66\" Y=\"47.565\" /><NE X=\"-0.40\" Y=\"47.56\" /><SE X=\"-0.41\" Y=\"47.39\" /><SO X=\"-0.67\" Y=\"47.395\" />" +
            "<ROUTES NbElements=\"1\">" +
            "<ROUTE NOM=\"A11\" LONGUEUR=\"172.5\" SITUATION=\"8\" NBR_VOIES=\"2\" SEPARATION=\"2\" REVETEMENT=\"2\" IMPORTANCE=\"14\" CATEGORIE=\"2\" LARGEUR_MAX=\"5\" LARGEUR=\"5\" MASSE_MAX=\"120\" SENS=\"1\">" +
            "<POINTS><POINT X=\"-0.50\" Y=\"47.50\" /><POINT X=\"-0.51\" Y=\"47.51\" /><POINT X=\"-0.52\" Y=\"47.515\" /></POINTS>" +
            "</ROUTE></ROUTES>" +
            "<VOIES_FERREES NbElements=\"1\">" +
            "<VOIE_FERREE NOM=\"vf\" LARGEUR=\"10\" NBRE_VOIES=\"1\" ECARTEMENT=\"1.5\">" +
            "<POINTS><POINT X=\"-0.45\" Y=\"47.45\" /><POINT X=\"-0.46\" Y=\"47.46\" /></POINTS>" +
            "</VOIE_FERREE></VOIES_FERREES>" +
            "<VEGETATIONS NbElements=\"1\">" +
            "<VEGETATION NOM=\"\" SURFACE=\"1000\" TYPE_VEGETATION=\"Arbres\" DENSITE=\"51\">" +
            "<CONTOUR><POINTS>" +
            "<POINT X=\"0\" Y=\"0\" /><POINT X=\"0.001\" Y=\"0\" /><POINT X=\"0.001\" Y=\"0.001\" /><POINT X=\"0\" Y=\"0.001\" /><POINT X=\"0\" Y=\"0\" />" +
            "</POINTS></CONTOUR>" +
            "<ZONES_EXCLUES><ZONE_EXCLUE><CONTOUR><POINTS>" +
            "<POINT X=\"0.0004\" Y=\"0.0004\" /><POINT X=\"0.0006\" Y=\"0.0004\" /><POINT X=\"0.0005\" Y=\"0.0006\" />" +
            "</POINTS></CONTOUR></ZONE_EXCLUE></ZONES_EXCLUES>" +
            "</VEGETATION></VEGETATIONS>" +
            "<PLANS_EAU NbElements=\"1\">" +
            "<PLAN_EAU HAUTEUR=\"0\" COMMENAIRE=\"lac\" NOM=\"Maine\" SURFACE=\"500\">" +
            "<CONTOUR><POINTS><POINT X=\"0.01\" Y=\"0.01\" /><POINT X=\"0.02\" Y=\"0.01\" /><POINT X=\"0.015\" Y=\"0.02\" /></POINTS></CONTOUR>" +
            "</PLAN_EAU></PLANS_EAU>" +
            "<BATIMENTS NbElements=\"1\">" +
            "<BATIMENT NOM=\"Bat1\" SURFACE=\"272.27\" HAUTEUR=\"10\" COMMENTAIRE=\"\" NB_NIVEAUX=\"4\">" +
            "<CONTOUR><POINTS><POINT X=\"0.03\" Y=\"0.03\" /><POINT X=\"0.04\" Y=\"0.03\" /><POINT X=\"0.035\" Y=\"0.04\" /></POINTS></CONTOUR>" +
            "</BATIMENT></BATIMENTS>" +
            "</PLANI_TYPE3>";

        public static Stream AsStream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

        [Test]
        public void Read_RootBoundsAndCorners()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(-0.40, map.LimEast, 1e-9);
            Assert.AreEqual(47.56, map.LimNorth, 1e-9);
            Assert.AreEqual(-0.67, map.LimWest, 1e-9);
            Assert.AreEqual(47.39, map.LimSouth, 1e-9);
            Assert.AreEqual(-0.66, map.CornerNW.Lon, 1e-9);
            Assert.AreEqual(47.395, map.CornerSW.Lat, 1e-9);
            Assert.AreEqual(-0.535, map.CenterLon, 1e-9);
            Assert.AreEqual(47.475, map.CenterLat, 1e-9);
        }

        [Test]
        public void Read_Road_AttributesAndPoints()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Roads.Count);
            var road = map.Roads[0];
            Assert.AreEqual("A11", road.Info.Name);
            Assert.AreEqual(172.5f, road.Info.Length, 1e-3f);
            Assert.AreEqual(14, road.Info.Importance);
            Assert.AreEqual(2, road.Info.LaneCount);
            Assert.AreEqual(5f, road.Info.Width, 1e-6f);
            Assert.AreEqual(1, road.Info.Direction);
            Assert.AreEqual(3, road.Points.Length);
            Assert.AreEqual(-0.51, road.Points[1].Lon, 1e-9);
            Assert.AreEqual(47.51, road.Points[1].Lat, 1e-9);
        }

        [Test]
        public void Read_Railway_Parsed()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Railways.Count);
            Assert.AreEqual(1.5f, map.Railways[0].Info.Gauge, 1e-6f);
            Assert.AreEqual(2, map.Railways[0].Points.Length);
        }

        [Test]
        public void Read_Vegetation_OuterRingNormalized_HoleCaptured()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Vegetation.Count);
            var veg = map.Vegetation[0];
            Assert.AreEqual("Arbres", veg.Info.VegetationType);
            Assert.AreEqual(51f, veg.Info.Density, 1e-6f);
            Assert.AreEqual(4, veg.Outer.Length, "closing duplicate point must be dropped");
            Assert.AreEqual(1, veg.Holes.Count);
            Assert.AreEqual(3, veg.Holes[0].Length);
        }

        [Test]
        public void Read_Water_CommenaireTypo_ReadAsComment()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Water.Count);
            Assert.AreEqual("lac", map.Water[0].Info.Comment);
            Assert.AreEqual("Maine", map.Water[0].Info.Name);
        }

        [Test]
        public void Read_Building_Parsed()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(1, map.Buildings.Count);
            Assert.AreEqual(4, map.Buildings[0].Info.Levels);
            Assert.AreEqual(10f, map.Buildings[0].Info.Height, 1e-6f);
            Assert.AreEqual(3, map.Buildings[0].Outer.Length);
        }

        [Test]
        public void Read_EmptySectionsAbsentSections_YieldEmptyLists()
        {
            var map = PlaniXmlReader.Read(AsStream(SampleXml));
            Assert.AreEqual(0, map.Bridges.Count);     // section absent entirely
            Assert.AreEqual(0, map.RiverLines.Count);  // section absent entirely
            Assert.AreEqual(0, map.Constructions.Count);
        }
    }
}
```

- [ ] **Step 4.2: Run, expect compile errors naming `PlaniXmlReader`** (verify ritual).

- [ ] **Step 4.3: Implement `Assets/Carto/Core/PlaniXmlReader.cs`:**

```csharp
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

                        case "ROUTE": map.Roads.Add(ReadLine(r, ReadRoadInfo)); break;
                        case "PONT_LINEAIRE": map.Bridges.Add(ReadLine(r, ReadBridgeInfo)); break;
                        case "FLEUVE_LINEAIRE": map.RiverLines.Add(ReadLine(r, ReadRiverInfo)); break;
                        case "VOIE_FERREE": map.Railways.Add(ReadLine(r, ReadRailwayInfo)); break;
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

        static GeoLine<T> ReadLine<T>(XmlReader r, Func<XmlReader, T> readInfo)
        {
            using (var sub = r.ReadSubtree())
            {
                sub.Read(); // position on the feature element itself
                var line = new GeoLine<T> { Info = readInfo(sub) };
                var pts = new List<GeoPoint>();
                while (sub.Read())
                    if (sub.NodeType == XmlNodeType.Element && sub.Name == "POINT")
                        pts.Add(new GeoPoint(DA(sub, "X"), DA(sub, "Y")));
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

                    var ring = NormalizeRing(ReadRing(sub));
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

        static GeoPoint[] ReadRing(XmlReader contourElement)
        {
            var pts = new List<GeoPoint>();
            using (var sub = contourElement.ReadSubtree())
            {
                while (sub.Read())
                    if (sub.NodeType == XmlNodeType.Element && sub.Name == "POINT")
                        pts.Add(new GeoPoint(DA(sub, "X"), DA(sub, "Y")));
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
```

- [ ] **Step 4.4: Run tests, expect pass** — `test_filter: "Snake2D.Tests.Carto.CartoPlaniReaderTests"`, 7/7 pass.

- [ ] **Step 4.5: Commit** (SNAKE):

```bash
git add Assets/Carto/Core/PlaniXmlReader.cs Assets/Tests/EditMode/Carto/CartoPlaniReaderTests.cs
git commit -m "feat(carto): streaming PLANI_TYPE3 parser — all 8 sections, rings with holes"
```

---

### Task 5: PlaniXmlReader — leniency pinning tests + coordinate-warning hardening

**Files:**
- Create: `Assets/Tests/EditMode/Carto/CartoLeniencyTests.cs`
- Modify: `Assets/Carto/Core/PlaniXmlReader.cs` (Step 5.0 — from Task 4's quality review)

These pin the leniency contract. Most should pass immediately (the behavior was built in Task 4); any red test is a parser bug — fix `PlaniXmlReader.cs`, don't weaken the test. Step 5.0 closes the one real gap Task 4's review found: an unparseable POINT coordinate silently became a (0,0) vertex with no diagnostic.

- [ ] **Step 5.0: Parser hardening — warn on missing/unparseable POINT coordinates.** In `PlaniXmlReader.cs`:

a) Change the four linear call sites in the main switch to pass warnings + label:

```csharp
                        case "ROUTE": map.Roads.Add(ReadLine(r, ReadRoadInfo, map.Warnings, "ROUTE")); break;
                        case "PONT_LINEAIRE": map.Bridges.Add(ReadLine(r, ReadBridgeInfo, map.Warnings, "PONT_LINEAIRE")); break;
                        case "FLEUVE_LINEAIRE": map.RiverLines.Add(ReadLine(r, ReadRiverInfo, map.Warnings, "FLEUVE_LINEAIRE")); break;
                        case "VOIE_FERREE": map.Railways.Add(ReadLine(r, ReadRailwayInfo, map.Warnings, "VOIE_FERREE")); break;
```

b) Change `ReadLine` and `ReadRing` signatures and point reads:

```csharp
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
```

```csharp
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
```

In `AddArea`, the ring read becomes `NormalizeRing(ReadRing(sub, warnings, label))`.

c) Add the two helpers next to the attribute readers (corner elements keep plain `DA` — real files always carry them and a zeroed corner is visible immediately):

```csharp
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
```

- [ ] **Step 5.1: Write the tests:**

```csharp
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
```

- [ ] **Step 5.2: Run tests** — full EditMode assembly. Expected: 10/10 CartoLeniencyTests pass and the whole suite is 30/30 (20 prior + 10 new). TDD note: `UnparseableCoordinate_WarnsAndDefaultsToZero` is the failing-first test for Step 5.0 — write the test file, see that one fail (the rest pass), then apply Step 5.0 and see 30/30.

- [ ] **Step 5.3: Commit** (SNAKE):

```bash
git add Assets/Carto/Core/PlaniXmlReader.cs Assets/Tests/EditMode/Carto/CartoLeniencyTests.cs Assets/Tests/EditMode/Carto/CartoLeniencyTests.cs.meta
git commit -m "test(carto): pin parser leniency contract + warn on unparseable POINT coordinates"
```

---

### Task 6: CartoMapData — bake + binary round-trip (TDD)

**Files:**
- Create: `Assets/Carto/Core/CartoMapData.cs`
- Create: `Assets/Tests/EditMode/Carto/CartoBakeBinaryTests.cs`

- [ ] **Step 6.1: Write the failing test** — `Assets/Tests/EditMode/Carto/CartoBakeBinaryTests.cs`:

```csharp
using System.IO;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoBakeBinaryTests
    {
        static CartoMapData BakeSample() =>
            CartoMapData.Bake(PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream(CartoPlaniReaderTests.SampleXml)), "sample");

        [Test]
        public void Bake_CenterIsOrigin_KnownPointProjectsToExpectedMeters()
        {
            var data = BakeSample();
            Assert.AreEqual(-0.535, data.CenterLon, 1e-9);
            Assert.AreEqual(47.475, data.CenterLat, 1e-9);
            // road point (-0.51, 47.51): dLon=0.025, dLat=0.035
            // x = 0.025 * cos(47.475°) * 111319.49 ≈ 1881.0 ; y = 0.035 * 111319.49 ≈ 3896.2
            var p = data.Roads[0].Points[1];
            Assert.AreEqual(1881.0f, p.X, 2f);
            Assert.AreEqual(3896.2f, p.Y, 2f);
        }

        [Test]
        public void Bake_AllLayersCarriedOver_WithHoles()
        {
            var data = BakeSample();
            Assert.AreEqual(1, data.Roads.Count);
            Assert.AreEqual(1, data.Railways.Count);
            Assert.AreEqual(1, data.Vegetation.Count);
            Assert.AreEqual(1, data.Vegetation[0].Holes.Length);
            Assert.AreEqual(1, data.Water.Count);
            Assert.AreEqual(1, data.Buildings.Count);
            Assert.AreEqual(0, data.Bridges.Count);
        }

        [Test]
        public void Bake_BoundsCoverAllGeometry()
        {
            var data = BakeSample();
            Assert.Less(data.BoundsMin.X, data.BoundsMax.X);
            Assert.Less(data.BoundsMin.Y, data.BoundsMax.Y);
            foreach (var r in data.Roads)
                foreach (var p in r.Points)
                {
                    Assert.GreaterOrEqual(p.X, data.BoundsMin.X);
                    Assert.LessOrEqual(p.Y, data.BoundsMax.Y);
                }
        }

        [Test]
        public void SaveLoad_RoundTrip_IsLossless()
        {
            var data = BakeSample();
            var ms = new MemoryStream();
            data.Save(ms);
            ms.Position = 0;
            var back = CartoMapData.Load(ms);

            Assert.AreEqual("sample", back.SourceName);
            Assert.AreEqual(data.CenterLon, back.CenterLon, 0.0);
            Assert.AreEqual(data.Roads.Count, back.Roads.Count);
            // whole-struct value equality — all fields of each Info survive, not a sample
            Assert.AreEqual(data.Roads[0].Info, back.Roads[0].Info);
            Assert.AreEqual(data.Railways[0].Info, back.Railways[0].Info);
            Assert.AreEqual(data.Vegetation[0].Info, back.Vegetation[0].Info);
            Assert.AreEqual(data.Water[0].Info, back.Water[0].Info);
            Assert.AreEqual(data.Buildings[0].Info, back.Buildings[0].Info);
            CollectionAssert.AreEqual(data.Roads[0].Points, back.Roads[0].Points);       // exact float bits
            CollectionAssert.AreEqual(data.Vegetation[0].Outer, back.Vegetation[0].Outer);
            Assert.AreEqual(data.Vegetation[0].Holes.Length, back.Vegetation[0].Holes.Length);
            CollectionAssert.AreEqual(data.Vegetation[0].Holes[0], back.Vegetation[0].Holes[0]);
            Assert.AreEqual(data.BoundsMax.X, back.BoundsMax.X);
        }

        [Test]
        public void Load_WrongMagic_Throws()
        {
            var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Assert.Throws<System.FormatException>(() => CartoMapData.Load(ms));
        }

        [Test]
        public void Load_CorruptCounts_ThrowFormatException_NotOOM()
        {
            var ms = new MemoryStream();
            BakeSample().Save(ms);
            var bytes = ms.ToArray();
            // First layer count (Roads) offset: magic(4)+version(2)+"sample"(1+6)+center(16)+bounds(16) = 45
            const int roadsCountOffset = 45;

            var huge = (byte[])bytes.Clone();
            System.BitConverter.GetBytes(int.MaxValue).CopyTo(huge, roadsCountOffset);
            Assert.Throws<System.FormatException>(() => CartoMapData.Load(new MemoryStream(huge)));

            var negative = (byte[])bytes.Clone();
            System.BitConverter.GetBytes(-5).CopyTo(negative, roadsCountOffset);
            Assert.Throws<System.FormatException>(() => CartoMapData.Load(new MemoryStream(negative)));
        }
    }
}
```

- [ ] **Step 6.2: Run, expect compile errors naming `CartoMapData`** (verify ritual).

- [ ] **Step 6.3: Implement `Assets/Carto/Core/CartoMapData.cs`:**

```csharp
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
```

- [ ] **Step 6.4: Run tests, expect pass** — `test_filter: "Snake2D.Tests.Carto.CartoBakeBinaryTests"`, 5/5 pass.

- [ ] **Step 6.5: Commit** (SNAKE):

```bash
git add Assets/Carto/Core/CartoMapData.cs Assets/Tests/EditMode/Carto/CartoBakeBinaryTests.cs
git commit -m "feat(carto): baked map model — projection bake, bounds, CMAP binary round-trip"
```

---

### Task 7: PolygonTriangulator (TDD)

**Files:**
- Create: `Assets/Carto/Core/PolygonTriangulator.cs`
- Create: `Assets/Tests/EditMode/Carto/CartoTriangulatorTests.cs`

- [ ] **Step 7.1: Write the failing test** — `Assets/Tests/EditMode/Carto/CartoTriangulatorTests.cs`:

```csharp
using System.Collections.Generic;
using Carto.Core;
using NUnit.Framework;
using V2 = System.Numerics.Vector2;

namespace Snake2D.Tests.Carto
{
    public class CartoTriangulatorTests
    {
        static float TriangleAreaSum(List<V2> v, List<int> idx)
        {
            float s = 0;
            for (int i = 0; i < idx.Count; i += 3)
            {
                V2 a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];
                s += System.Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * 0.5f;
            }
            return s;
        }

        static (List<V2>, List<int>, int) Run(V2[] outer, V2[][] holes)
        {
            var verts = new List<V2>();
            var idx = new List<int>();
            Assert.IsTrue(PolygonTriangulator.Triangulate(outer, holes, verts, idx, out var dropped));
            Assert.AreEqual(0, idx.Count % 3);
            return (verts, idx, dropped);
        }

        [Test]
        public void UnitSquare_TwoTriangles_AreaOne()
        {
            var (v, idx, _) = Run(new[] { new V2(0, 0), new V2(1, 0), new V2(1, 1), new V2(0, 1) }, null);
            Assert.AreEqual(6, idx.Count);
            Assert.AreEqual(1f, TriangleAreaSum(v, idx), 1e-4f);
        }

        [Test]
        public void ClockwiseInput_Normalized_SameArea()
        {
            var (v, idx, _) = Run(new[] { new V2(0, 1), new V2(1, 1), new V2(1, 0), new V2(0, 0) }, null);
            Assert.AreEqual(1f, TriangleAreaSum(v, idx), 1e-4f);
        }

        [Test]
        public void ConcaveL_Shape_AreaThree()
        {
            // L covering 3 unit squares: (0,0)-(2,0)-(2,1)-(1,1)-(1,2)-(0,2)
            var outer = new[] { new V2(0, 0), new V2(2, 0), new V2(2, 1), new V2(1, 1), new V2(1, 2), new V2(0, 2) };
            var (v, idx, _) = Run(outer, null);
            Assert.AreEqual(3f, TriangleAreaSum(v, idx), 1e-4f);
        }

        [Test]
        public void SquareWithSquareHole_AreaIsDifference()
        {
            var outer = new[] { new V2(0, 0), new V2(4, 0), new V2(4, 4), new V2(0, 4) };
            var hole = new[] { new V2(1, 1), new V2(3, 1), new V2(3, 3), new V2(1, 3) };
            var (v, idx, dropped) = Run(outer, new[] { hole });
            Assert.AreEqual(16f - 4f, TriangleAreaSum(v, idx), 1e-3f);
            Assert.AreEqual(0, dropped);
        }

        [Test]
        public void TwoDisjointHoles_AreaIsDifference_NoneDropped()
        {
            var outer = new[] { new V2(0, 0), new V2(8, 0), new V2(8, 8), new V2(0, 8) };
            var h1 = new[] { new V2(1, 1), new V2(3, 1), new V2(3, 3), new V2(1, 3) };
            var h2 = new[] { new V2(5, 5), new V2(7, 5), new V2(7, 7), new V2(5, 7) };
            var (v, idx, dropped) = Run(outer, new[] { h1, h2 });
            Assert.AreEqual(64f - 4f - 4f, TriangleAreaSum(v, idx), 1e-3f);
            Assert.AreEqual(0, dropped);
        }

        [Test]
        public void HoleSharingVertexWithOuter_DroppedAndCounted_NoCorruption()
        {
            // Real cadastral holes can touch the outer ring. A coincident vertex cannot be
            // bridged without a self-touching polygon (ear-clip area inflation), so the
            // hole is dropped (counted) and the feature fills over it.
            var outer = new[] { new V2(0, 0), new V2(4, 0), new V2(4, 4), new V2(0, 4) };
            var hole = new[] { new V2(0, 0), new V2(1, 0.5f), new V2(0.5f, 1) };
            var verts = new List<V2>();
            var idx = new List<int>();
            var ok = PolygonTriangulator.Triangulate(outer, new[] { hole }, verts, idx, out var dropped);
            Assert.IsTrue(ok);
            Assert.AreEqual(1, dropped);
            Assert.AreEqual(0, idx.Count % 3);
            Assert.AreEqual(16f, TriangleAreaSum(verts, idx), 1e-3f);
        }

        [Test]
        public void DegenerateInput_ReturnsFalse()
        {
            var verts = new List<V2>();
            var idx = new List<int>();
            Assert.IsFalse(PolygonTriangulator.Triangulate(new[] { new V2(0, 0), new V2(1, 1) }, null, verts, idx));
        }
    }
}
```

- [ ] **Step 7.2: Run, expect compile errors naming `PolygonTriangulator`** (verify ritual).

- [ ] **Step 7.3: Implement `Assets/Carto/Core/PolygonTriangulator.cs`:**

```csharp
using System.Collections.Generic;
using System.Numerics;

namespace Carto.Core
{
    /// <summary>
    /// Ear-clipping triangulation with hole support (holes bridged into the outer
    /// ring before clipping). Import-time / load-time use only — O(n²), fine for
    /// BD TOPO-scale polygons (tens to low thousands of vertices).
    /// </summary>
    public static class PolygonTriangulator
    {
        /// <summary>
        /// Triangulates outer (any winding) minus holes. Appends the merged vertex
        /// list to outVerts and triangle indices (CCW) to outIndices.
        /// Returns false on degenerate input; both lists are cleared first either way.
        /// droppedHoles counts holes that were degenerate or could not be bridged and
        /// were filled over — callers must surface it, never swallow it.
        /// Near-degenerate sliver triangles are expected and harmless in a flat fill.
        /// Complexity ~O(n²) realistic, O(n³) pathological — import/load-time use only.
        /// </summary>
        public static bool Triangulate(Vector2[] outer, Vector2[][] holes, List<Vector2> outVerts, List<int> outIndices, out int droppedHoles)
        {
            outVerts.Clear();
            outIndices.Clear();
            droppedHoles = 0;
            if (outer == null || outer.Length < 3) return false;

            var ring = new List<Vector2>(outer);
            if (SignedArea(ring) < 0) ring.Reverse(); // outer CCW

            if (holes != null && holes.Length > 0)
            {
                var holeList = new List<List<Vector2>>();
                foreach (var h in holes)
                {
                    if (h == null || h.Length < 3) { droppedHoles++; continue; }
                    var hl = new List<Vector2>(h);
                    if (SignedArea(hl) > 0) hl.Reverse(); // holes CW
                    holeList.Add(hl);
                }
                holeList.Sort((a, b) => MaxX(b).CompareTo(MaxX(a))); // rightmost hole first
                foreach (var h in holeList) ring = MergeHole(ring, h, ref droppedHoles);
            }

            return EarClip(ring, outVerts, outIndices);
        }

        /// <summary>Convenience overload when the caller does not track dropped holes.</summary>
        public static bool Triangulate(Vector2[] outer, Vector2[][] holes, List<Vector2> outVerts, List<int> outIndices)
            => Triangulate(outer, holes, outVerts, outIndices, out _);

        static float SignedArea(List<Vector2> ring)
        {
            float s = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                s += a.X * b.Y - b.X * a.Y;
            }
            return s * 0.5f;
        }

        static float MaxX(List<Vector2> ring)
        {
            float m = ring[0].X;
            for (int i = 1; i < ring.Count; i++) if (ring[i].X > m) m = ring[i].X;
            return m;
        }

        static List<Vector2> MergeHole(List<Vector2> ring, List<Vector2> hole, ref int droppedHoles)
        {
            // A hole vertex sitting exactly on the ring (outer or an already-merged hole)
            // cannot be bridged without producing a self-touching polygon — any bridge
            // choice still leaves that coincident point visited twice at non-adjacent
            // positions, which the ear-clipper's bridge-duplicate exception can mistake
            // for a legitimate ear and overcount area. Drop it rather than risk that.
            for (int i = 0; i < hole.Count; i++)
                for (int j = 0; j < ring.Count; j++)
                    if (Same(hole[i], ring[j])) { droppedHoles++; return ring; }

            int hi = 0;
            for (int i = 1; i < hole.Count; i++) if (hole[i].X > hole[hi].X) hi = i;
            var h = hole[hi];

            var order = new List<int>(ring.Count);
            for (int i = 0; i < ring.Count; i++) order.Add(i);
            order.Sort((a, b) => (ring[a] - h).LengthSquared().CompareTo((ring[b] - h).LengthSquared()));

            foreach (var ri in order)
            {
                if (!SegmentClear(ring, hole, h, ring[ri])) continue;
                // splice: ..ring[ri], hole[hi..] wrapped back to hole[hi], ring[ri], rest of ring
                var merged = new List<Vector2>(ring.Count + hole.Count + 2);
                for (int i = 0; i <= ri; i++) merged.Add(ring[i]);
                for (int i = 0; i <= hole.Count; i++) merged.Add(hole[(hi + i) % hole.Count]);
                merged.Add(ring[ri]);
                for (int i = ri + 1; i < ring.Count; i++) merged.Add(ring[i]);
                return merged;
            }
            droppedHoles++;
            return ring; // unbridgeable hole — dropped (counted), rendering-grade fallback
        }

        static bool SegmentClear(List<Vector2> ring, List<Vector2> hole, Vector2 a, Vector2 b)
        {
            for (int i = 0; i < ring.Count; i++)
                if (ProperIntersect(a, b, ring[i], ring[(i + 1) % ring.Count])) return false;
            for (int i = 0; i < hole.Count; i++)
                if (ProperIntersect(a, b, hole[i], hole[(i + 1) % hole.Count])) return false;
            return true;
        }

        static bool ProperIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            if (Same(p1, q1) || Same(p1, q2) || Same(p2, q1) || Same(p2, q2)) return false;
            float d1 = Cross(q2 - q1, p1 - q1);
            float d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1);
            float d4 = Cross(p2 - p1, q2 - p1);
            return ((d1 > 0) != (d2 > 0)) && ((d3 > 0) != (d4 > 0));
        }

        // Effectively exact-equality at map scale (1e-6 m << float ULP at 10 km) — used only
        // on bit-identical bridge duplicates; do NOT reuse as a geometric proximity tolerance.
        static bool Same(Vector2 a, Vector2 b) => (a - b).LengthSquared() < 1e-12f;
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        static bool EarClip(List<Vector2> poly, List<Vector2> outVerts, List<int> outIndices)
        {
            outVerts.AddRange(poly);
            var idx = new List<int>(poly.Count);
            for (int i = 0; i < poly.Count; i++) idx.Add(i);

            int guard = poly.Count * poly.Count + 16;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = outVerts[i0], b = outVerts[i1], c = outVerts[i2];
                    if (Cross(b - a, c - a) <= 1e-12f) continue; // reflex or collinear — not an ear

                    bool contains = false;
                    for (int j = 0; j < idx.Count; j++)
                    {
                        int p = idx[j];
                        if (p == i0 || p == i1 || p == i2) continue;
                        var q = outVerts[p];
                        // bridge duplicates share coordinates with corners — not blockers
                        if (Same(q, a) || Same(q, b) || Same(q, c)) continue;
                        if (PointInTriangle(q, a, b, c)) { contains = true; break; }
                    }
                    if (contains) continue;

                    outIndices.Add(i0); outIndices.Add(i1); outIndices.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // no ear found (self-intersecting input) — partial result
            }

            if (idx.Count == 3)
            {
                outIndices.Add(idx[0]); outIndices.Add(idx[1]); outIndices.Add(idx[2]);
                return true;
            }
            return idx.Count < 3;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0; // CCW triangle, edge-inclusive
        }
    }
}
```

- [ ] **Step 7.4: Run tests, expect pass** — `test_filter: "Snake2D.Tests.Carto.CartoTriangulatorTests"`, 5/5 pass.

- [ ] **Step 7.5: Commit** (SNAKE):

```bash
git add Assets/Carto/Core/PolygonTriangulator.cs Assets/Tests/EditMode/Carto/CartoTriangulatorTests.cs
git commit -m "feat(carto): ear-clipping triangulator with hole bridging"
```

---

### Task 8: Runtime assembly — CartoMapAsset + CartoMeshBuilder (TDD)

**Files:**
- Create: `Assets/Carto/Runtime/Carto.Unity.Runtime.asmdef`, `Assets/Carto/Runtime/CartoMapAsset.cs`, `Assets/Carto/Runtime/CartoMeshBuilder.cs`
- Create: `Assets/Tests/EditMode/Carto/CartoMeshBuilderTests.cs`

**Winding rule (why the tests check cross products):** the default 2D camera sits at z = −10 looking toward +Z. Unity's visible faces there are triangles whose XY winding is *clockwise* (negative z cross product) — Unity's own Quad primitive uses exactly that. Sprites don't care (sprite shaders cull off), but MeshRenderer + URP Unlit culls back faces, so every triangle we emit must be CW. The triangulator outputs CCW → the mesh builder reverses.

- [ ] **Step 8.1: Create `Assets/Carto/Runtime/Carto.Unity.Runtime.asmdef`:**

```json
{
    "name": "Carto.Unity.Runtime",
    "rootNamespace": "Carto.Unity",
    "references": [
        "Carto.Core"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Also update `Assets/Tests/EditMode/Snake2D.EditMode.Tests.asmdef` — the `references` array becomes:

```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Snake2D",
        "Carto.Core",
        "Carto.Unity.Runtime"
    ],
```

- [ ] **Step 8.2: Write the failing test** — `Assets/Tests/EditMode/Carto/CartoMeshBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using Carto.Unity;
using NUnit.Framework;
using UnityEngine;
using SysV2 = System.Numerics.Vector2;

namespace Snake2D.Tests.Carto
{
    public class CartoMeshBuilderTests
    {
        static void AssertAllTrianglesClockwise(Mesh mesh)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                float cross = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                Assert.Less(cross, 0f, "triangle " + i / 3 + " must be CW (visible to the 2D camera)");
            }
        }

        [Test]
        public void BuildPolylines_SingleSegment_QuadWithWidth()
        {
            var lines = new List<(SysV2[] points, float width)>
            {
                (new[] { new SysV2(0, 0), new SysV2(10, 0) }, 2f)
            };
            var mesh = CartoMeshBuilder.BuildPolylines(lines, "test");
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            // width 2 → offsets ±1 in Y for a horizontal segment
            Assert.AreEqual(-1f, mesh.bounds.min.y, 1e-4f);
            Assert.AreEqual(1f, mesh.bounds.max.y, 1e-4f);
            Assert.AreEqual(10f, mesh.bounds.max.x, 1e-4f);
            AssertAllTrianglesClockwise(mesh);
        }

        [Test]
        public void BuildPolylines_ZeroLengthSegments_Skipped()
        {
            var p = new SysV2(3, 3);
            var lines = new List<(SysV2[] points, float width)> { (new[] { p, p, p }, 5f) };
            var mesh = CartoMeshBuilder.BuildPolylines(lines, "test");
            Assert.AreEqual(0, mesh.vertexCount);
        }

        [Test]
        public void BuildPolygons_UnitSquare_VisibleWinding()
        {
            var areas = new List<(SysV2[] outer, SysV2[][] holes)>
            {
                (new[] { new SysV2(0, 0), new SysV2(1, 0), new SysV2(1, 1), new SysV2(0, 1) }, null)
            };
            var mesh = CartoMeshBuilder.BuildPolygons(areas, "test");
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            AssertAllTrianglesClockwise(mesh);
        }

        [Test]
        public void BuildPolygons_ManyAreas_IndicesOffsetCorrectly()
        {
            var square = new[] { new SysV2(0, 0), new SysV2(1, 0), new SysV2(1, 1), new SysV2(0, 1) };
            var far = new[] { new SysV2(10, 10), new SysV2(11, 10), new SysV2(11, 11), new SysV2(10, 11) };
            var mesh = CartoMeshBuilder.BuildPolygons(
                new List<(SysV2[] outer, SysV2[][] holes)> { (square, null), (far, null) }, "test");
            Assert.AreEqual(8, mesh.vertexCount);
            Assert.AreEqual(12, mesh.triangles.Length);
            foreach (var idx in mesh.triangles) Assert.Less(idx, 8);
            AssertAllTrianglesClockwise(mesh);
        }

        [Test]
        public void BuildPolygons_ReportsDroppedGeometry()
        {
            var good = new[] { new SysV2(0, 0), new SysV2(1, 0), new SysV2(1, 1), new SysV2(0, 1) };
            var degenerate = new[] { new SysV2(0, 0), new SysV2(1, 1) };
            var mesh = CartoMeshBuilder.BuildPolygons(
                new List<(SysV2[] outer, SysV2[][] holes)> { (good, null), (degenerate, null) }, "test",
                out var droppedHoles, out var droppedPolygons);
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(0, droppedHoles);
            Assert.AreEqual(1, droppedPolygons);
        }

        [Test]
        public void BuildPolylines_Above65535Vertices_SwitchesToUInt32()
        {
            // 20,000 one-segment lines → 80,000 verts: crosses the 16-bit index boundary —
            // the one Angers-scale behavior this layer owns.
            var lines = new List<(SysV2[] points, float width)>(20000);
            for (int i = 0; i < 20000; i++)
                lines.Add((new[] { new SysV2(i, 0), new SysV2(i, 1) }, 1f));
            var mesh = CartoMeshBuilder.BuildPolylines(lines, "big");
            Assert.AreEqual(80000, mesh.vertexCount);
            Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt32, mesh.indexFormat);
            int maxIndex = 0;
            foreach (var idx in mesh.triangles) if (idx > maxIndex) maxIndex = idx;
            Assert.Greater(maxIndex, 65535);
            Assert.Less(maxIndex, 80000);
        }
    }
}
```

- [ ] **Step 8.3: Run, expect compile errors naming `CartoMeshBuilder`** (verify ritual).

- [ ] **Step 8.4: Implement `Assets/Carto/Runtime/CartoMapAsset.cs`:**

```csharp
using System.IO;
using Carto.Core;
using UnityEngine;

namespace Carto.Unity
{
    /// <summary>Loads a baked .cartomap.bytes TextAsset. No geo-processing at runtime.</summary>
    public static class CartoMapAsset
    {
        public static CartoMapData Load(TextAsset asset)
        {
            using (var ms = new MemoryStream(asset.bytes, writable: false))
                return CartoMapData.Load(ms);
        }
    }
}
```

- [ ] **Step 8.5: Implement `Assets/Carto/Runtime/CartoMeshBuilder.cs`:**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SysV2 = System.Numerics.Vector2;

namespace Carto.Unity
{
    /// <summary>
    /// Flat 2D mesh construction in the XY plane (z = 0), 1 unit = 1 m.
    /// All emitted triangles wind clockwise in XY — visible to a camera at −Z
    /// looking +Z with back-face culling on (see CartoMeshBuilderTests).
    /// </summary>
    public static class CartoMeshBuilder
    {
        public static Mesh BuildPolylines(IReadOnlyList<(SysV2[] points, float width)> lines, string name)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var (points, width) in lines)
            {
                if (points == null || points.Length < 2) continue;
                float half = Mathf.Max(width, 0.5f) * 0.5f;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var a = new Vector2(points[i].X, points[i].Y);
                    var b = new Vector2(points[i + 1].X, points[i + 1].Y);
                    var d = b - a;
                    float len = d.magnitude;
                    if (len < 1e-4f) continue;
                    var n = new Vector2(-d.y, d.x) / len * half;
                    int v0 = verts.Count;
                    verts.Add(a - n); verts.Add(a + n); verts.Add(b - n); verts.Add(b + n);
                    // CW pairs for a camera looking +Z:
                    tris.Add(v0); tris.Add(v0 + 1); tris.Add(v0 + 2);
                    tris.Add(v0 + 1); tris.Add(v0 + 3); tris.Add(v0 + 2);
                }
            }
            return ToMesh(verts, tris, name);
        }

        public static Mesh BuildPolygons(IReadOnlyList<(SysV2[] outer, SysV2[][] holes)> areas, string name)
            => BuildPolygons(areas, name, out _, out _);

        public static Mesh BuildPolygons(IReadOnlyList<(SysV2[] outer, SysV2[][] holes)> areas, string name,
            out int droppedHoles, out int droppedPolygons)
        {
            droppedHoles = 0;
            droppedPolygons = 0;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var polyVerts = new List<SysV2>();
            var polyIdx = new List<int>();
            foreach (var (outer, holes) in areas)
            {
                if (!Carto.Core.PolygonTriangulator.Triangulate(outer, holes, polyVerts, polyIdx, out var dropped))
                {
                    droppedPolygons++;
                    continue;
                }
                droppedHoles += dropped;
                int baseIndex = verts.Count;
                foreach (var p in polyVerts) verts.Add(new Vector3(p.X, p.Y, 0f));
                // triangulator emits CCW → reverse to CW for visibility
                for (int i = 0; i < polyIdx.Count; i += 3)
                {
                    tris.Add(baseIndex + polyIdx[i]);
                    tris.Add(baseIndex + polyIdx[i + 2]);
                    tris.Add(baseIndex + polyIdx[i + 1]);
                }
            }
            return ToMesh(verts, tris, name);
        }

        static Mesh ToMesh(List<Vector3> verts, List<int> tris, string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0); // calculateBounds defaults to true — no extra pass needed
            return mesh;
        }
    }
}
```

- [ ] **Step 8.6: Run tests, expect pass** — `test_filter: "Snake2D.Tests.Carto.CartoMeshBuilderTests"`, 4/4 pass.

- [ ] **Step 8.7: Commit** (SNAKE):

```bash
git add Assets/Carto/Runtime Assets/Tests/EditMode/Carto/CartoMeshBuilderTests.cs
git commit -m "feat(carto): runtime assembly — TextAsset loader, CW polyline/polygon mesh builder"
```

---

### Task 9: CartoMapRenderer (TDD via BuildFrom)

**Files:**
- Create: `Assets/Carto/Runtime/CartoMapRenderer.cs`
- Create: `Assets/Tests/EditMode/Carto/CartoMapRendererTests.cs`

- [ ] **Step 9.1: Write the failing test** — `Assets/Tests/EditMode/Carto/CartoMapRendererTests.cs`:

```csharp
using Carto.Core;
using Carto.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Snake2D.Tests.Carto
{
    public class CartoMapRendererTests
    {
        [Test]
        public void BuildFrom_SampleMap_CreatesLayerObjectsWithMeshes()
        {
            var data = CartoMapData.Bake(
                PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream(CartoPlaniReaderTests.SampleXml)), "sample");
            var go = new GameObject("map");
            try
            {
                var renderer = go.AddComponent<CartoMapRenderer>();
                renderer.BuildFrom(data);

                var root = go.transform.Find("__CartoLayers");
                Assert.IsNotNull(root, "layer root must exist");
                Assert.Greater(root.childCount, 0);

                int meshedLayers = 0;
                foreach (Transform child in root)
                {
                    var mf = child.GetComponent<MeshFilter>();
                    Assert.IsNotNull(mf);
                    if (mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0) meshedLayers++;
                }
                // sample has: 1 road (importance 14 → highway bucket), 1 railway,
                // 1 vegetation, 1 water area, 1 building → at least 5 non-empty layers
                Assert.GreaterOrEqual(meshedLayers, 5);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildFrom_Twice_DoesNotAccumulateRoots()
        {
            var data = CartoMapData.Bake(
                PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream(CartoPlaniReaderTests.SampleXml)), "sample");
            var go = new GameObject("map");
            try
            {
                var renderer = go.AddComponent<CartoMapRenderer>();
                renderer.BuildFrom(data);
                renderer.BuildFrom(data);
                int roots = 0;
                foreach (Transform child in go.transform)
                    if (child.name == "__CartoLayers") roots++;
                Assert.AreEqual(1, roots);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildFrom_Rebuild_FreesPreviousMeshes()
        {
            var data = CartoMapData.Bake(
                PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream(CartoPlaniReaderTests.SampleXml)), "sample");
            var go = new GameObject("map");
            try
            {
                var renderer = go.AddComponent<CartoMapRenderer>();
                renderer.BuildFrom(data);
                var oldMeshes = new System.Collections.Generic.List<Mesh>();
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    oldMeshes.Add(mf.sharedMesh);
                Assert.Greater(oldMeshes.Count, 0);

                renderer.BuildFrom(data);
                foreach (var m in oldMeshes)
                    Assert.IsTrue(m == null, "previous-generation mesh must be destroyed on rebuild");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildFrom_LayerZOrder_BuildingsInFrontOfRoadsInFrontOfWater()
        {
            var data = CartoMapData.Bake(
                PlaniXmlReader.Read(CartoPlaniReaderTests.AsStream(CartoPlaniReaderTests.SampleXml)), "sample");
            var go = new GameObject("map");
            try
            {
                var renderer = go.AddComponent<CartoMapRenderer>();
                renderer.BuildFrom(data);
                var root = go.transform.Find("__CartoLayers");
                float Z(string n) => root.Find(n).localPosition.z;
                Assert.Less(Z("Buildings"), Z("RoadsHighway")); // smaller z = closer to the −Z camera
                Assert.Less(Z("RoadsHighway"), Z("Water"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

- [ ] **Step 9.2: Run, expect compile errors naming `CartoMapRenderer`** (verify ritual).

- [ ] **Step 9.3: Implement `Assets/Carto/Runtime/CartoMapRenderer.cs`:**

```csharp
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
```

- [ ] **Step 9.4: Run tests, expect pass** — `test_filter: "Snake2D.Tests.Carto.CartoMapRendererTests"`, 2/2 pass. Then run the whole suite once (`test_filter: "Snake2D.Tests.Carto"`) — everything green so far.

- [ ] **Step 9.5: Commit** (SNAKE):

```bash
git add Assets/Carto/Runtime/CartoMapRenderer.cs Assets/Tests/EditMode/Carto/CartoMapRendererTests.cs
git commit -m "feat(carto): CartoMapRenderer — load-time layer meshes, transient children, style table"
```

---

### Task 10: Editor assembly — import pipeline, window, scene builder

**Files:**
- Create: `Assets/Carto/Editor/Carto.Unity.Editor.asmdef`, `Assets/Carto/Editor/CartoImportPipeline.cs`, `Assets/Carto/Editor/CartoImportWindow.cs`, `Assets/Carto/Editor/CartoSceneBuilder.cs`

Thin editor shell over already-tested Core/Runtime code — no unit tests here; Task 11 (integration) and Task 12 (live import) are the gates. Tests never reference this assembly.

- [ ] **Step 10.1: Create `Assets/Carto/Editor/Carto.Unity.Editor.asmdef`:**

```json
{
    "name": "Carto.Unity.Editor",
    "rootNamespace": "Carto.Unity.Editor",
    "references": [
        "Carto.Core",
        "Carto.Unity.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 10.2: Create `Assets/Carto/Editor/CartoImportPipeline.cs`:**

```csharp
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
```

- [ ] **Step 10.3: Create `Assets/Carto/Editor/CartoSceneBuilder.cs`:**

```csharp
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
            cam.orthographicSize = Mathf.Max(span.Y * 0.55f, 100f);
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
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
```

- [ ] **Step 10.4: Create `Assets/Carto/Editor/CartoImportWindow.cs`:**

```csharp
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
                    progress: (msg, t) => EditorUtility.DisplayProgressBar("Carto Import", msg, t));
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
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
```

- [ ] **Step 10.5: Verify ritual** (compile-wait + empty console). Confirm the menu exists: `Unity Kit → Carto → Import PLANI Map...`.

- [ ] **Step 10.6: Commit** (SNAKE):

```bash
git add Assets/Carto/Editor
git commit -m "feat(carto): editor import pipeline, import window, demo scene builder"
```

---

### Task 11: Machine-local integration test (real Angers data)

**Files:**
- Create: `Assets/Tests/EditMode/Carto/CartoAngersIntegrationTests.cs`

Core-level end-to-end (parse → bake → binary round-trip) against the real 29 MB file. Auto-ignores when the dataset is absent → CI-safe, and no editor-assembly reference needed from tests.

- [ ] **Step 11.1: Write the test:**

```csharp
using System.IO;
using Carto.Core;
using NUnit.Framework;

namespace Snake2D.Tests.Carto
{
    public class CartoAngersIntegrationTests
    {
        const string DataDir = @"C:\Users\bencu\unityProjects\snake-unity-kit\Carto\carte angers";
        static string XmlPath => Path.Combine(DataDir, "Angers2.xml");
        static string GeoPath => Path.Combine(DataDir, "angersZUB.geo");

        [Test]
        public void ImportAngers2_EndToEnd_ParseBakeRoundTrip()
        {
            if (!File.Exists(XmlPath)) Assert.Ignore("Angers dataset not present on this machine");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            PlaniMap map;
            using (var fs = File.OpenRead(XmlPath)) map = PlaniXmlReader.Read(fs);
            TestContext.WriteLine($"parse: {sw.Elapsed.TotalSeconds:F1}s, warnings: {map.Warnings.Count}");

            Assert.Greater(map.Roads.Count, 1000, "Angers2 should contain thousands of roads");
            Assert.Less(map.LimWest, map.LimEast);
            Assert.That(map.CenterLat, Is.InRange(47.0, 48.0), "Angers sits at ~47.47°N");

            sw.Restart();
            var baked = CartoMapData.Bake(map, "Angers2");
            TestContext.WriteLine($"bake: {sw.Elapsed.TotalSeconds:F1}s");
            var span = baked.BoundsMax - baked.BoundsMin;
            Assert.That(span.X, Is.InRange(1000f, 40000f), "map should span kilometers east-west");
            Assert.That(span.Y, Is.InRange(1000f, 40000f), "map should span kilometers north-south");

            sw.Restart();
            var ms = new MemoryStream();
            baked.Save(ms);
            ms.Position = 0;
            var back = CartoMapData.Load(ms);
            TestContext.WriteLine($"binary round-trip: {sw.Elapsed.TotalSeconds:F1}s, {ms.Length / (1024 * 1024)} MB");
            Assert.AreEqual(baked.Roads.Count, back.Roads.Count);
            Assert.AreEqual(baked.Vegetation.Count, back.Vegetation.Count);
            Assert.AreEqual(baked.Buildings.Count, back.Buildings.Count);
        }

        [Test]
        public void AngersGeoSidecar_Parses()
        {
            if (!File.Exists(GeoPath)) Assert.Ignore("Angers dataset not present on this machine");
            GeoReference geo;
            using (var fs = File.OpenRead(GeoPath)) geo = GeoReference.Read(fs);
            Assert.AreEqual(1.5057104143025681, geo.PixelMetreX, 1e-9);
            Assert.AreEqual(12825, geo.DimensionImageX);
            Assert.AreEqual(12544, geo.DimensionImageY);
            Assert.That(geo.WidthMeters, Is.InRange(19000.0, 19700.0));
        }
    }
}
```

- [ ] **Step 11.2: Run** — `test_filter: "Snake2D.Tests.Carto.CartoAngersIntegrationTests"`. Expected: **2/2 pass on this machine** (dataset present) with timings printed. If assertions about counts/spans fail, the real format diverges from the fixture assumptions — investigate the parser (likely a section-name or attribute variant), don't loosen the assertion blindly.

- [ ] **Step 11.3: Commit** (SNAKE):

```bash
git add Assets/Tests/EditMode/Carto/CartoAngersIntegrationTests.cs
git commit -m "test(carto): machine-local Angers2 end-to-end integration (auto-ignored without dataset)"
```

---

### Task 12: Live import — scene, play smoke, screenshot

**Files:**
- Create (generated): `Assets/Scenes/CartoAngers2.unity` (committed), `Assets/CartoMaps/*` (gitignored)

- [ ] **Step 12.1: Run the full carto suite once** — `test_filter: "Snake2D.Tests.Carto"`. Expected: all green (≈31 tests, 2 may be Ignored only if the dataset moved).

- [ ] **Step 12.2: Execute the import headlessly** via `mcp__UnityMCP__execute_code` (activate the `scripting_ext` tool group via `manage_tools` if needed):

```csharp
var result = Carto.Unity.Editor.CartoImportPipeline.Import(
    @"C:\Users\bencu\unityProjects\snake-unity-kit\Carto\carte angers\Angers2.xml",
    @"C:\Users\bencu\unityProjects\snake-unity-kit\Carto\carte angers\angersZUB.geo",
    @"C:\Users\bencu\unityProjects\snake-unity-kit\Carto\carte angers\angersZUB.tif");
var scenePath = Carto.Unity.Editor.CartoSceneBuilder.BuildScene(result);
UnityEngine.Debug.Log("[Carto] demo scene: " + scenePath + ", bytes: " + result.BytesAssetPath);
```

Expected console: the `[Carto] Imported Angers2 in …` summary line with non-zero roads/vegetation counts, then `[Carto] demo scene: Assets/Scenes/CartoAngers2.unity`. Raster texture import (12825×12544 tif) may take a minute.

- [ ] **Step 12.3: Read the console** (`read_console`) — no errors; note the summary numbers for the final report.

- [ ] **Step 12.4: Screenshot the scene** — use the unity-kit verify tooling (`unity-kit:unity-verify` skill step or `manage_camera` screenshot) to capture the Game/Scene view of `CartoAngers2.unity` into `Captures/carto-angers2.png`. The image must show the raster underlay with vector layers (roads/vegetation/water) registered on top.

- [ ] **Step 12.5: Play-mode smoke** — `manage_editor` enter play (exercises `CartoMapRenderer.Awake` → runtime mesh build), wait ~5 s, `read_console` (no errors, no exceptions), exit play.

- [ ] **Step 12.6: Sanity-check scene file size** — `CartoAngers2.unity` must be small (a few KB, no serialized meshes):

```bash
ls -la Assets/Scenes/CartoAngers2.unity
```

- [ ] **Step 12.7: Commit** (SNAKE) — scene + meta only; `Assets/CartoMaps/` stays ignored:

```bash
git add Assets/Scenes/CartoAngers2.unity Assets/Scenes/CartoAngers2.unity.meta
git commit -m "feat(carto): CartoAngers2 demo scene — renderer + georeferenced raster underlay"
```

---

### Task 13: Sync sources into the plugin template

**Files:**
- Create: `unity-kit/templates/carto/README.md` (PLUGIN) + synced `Core/ Runtime/ Editor/ Tests/EditMode/` source trees

- [ ] **Step 13.1: Copy sources (no `.meta` files) from SNAKE into PLUGIN:**

```bash
SRC="C:/Users/bencu/unityProjects/snake-unity-kit/.claude/worktrees/carto-map-unity-integration-69e523/Assets"
DST="C:/Users/bencu/claude-plugins-carto/unity-kit/templates/carto"
mkdir -p "$DST/Tests/EditMode"
cp -r "$SRC/Carto/Core" "$SRC/Carto/Runtime" "$SRC/Carto/Editor" "$DST/"
cp -r "$SRC/Tests/EditMode/Carto/." "$DST/Tests/EditMode/"
find "$DST" -name "*.meta" -delete
```

- [ ] **Step 13.2: Create `unity-kit/templates/carto/README.md`:**

```markdown
# Carto template — PLANI_TYPE3 maps in Unity

Reusable importer for the PLANI_TYPE3/"carto" map format family (see the
`unity-carto-maps` skill for the format reference and workflow).

## What's here

- `Core/` → copy to `Assets/Carto/Core/` — engine-free (.NET Standard 2.1, zero
  `UnityEngine` refs): streaming PLANI_TYPE3 parser, `.geo` parser, tangent-plane
  projection, baked `CartoMapData` + CMAP binary, ear-clipping triangulator.
- `Runtime/` → copy to `Assets/Carto/Runtime/` — `CartoMapAsset.Load(TextAsset)`,
  `CartoMeshBuilder` (CW winding for the 2D camera), `CartoMapRenderer`
  (builds layer meshes at load; children are DontSave — scenes stay small).
- `Editor/` → copy to `Assets/Carto/Editor/` — `Unity Kit → Carto → Import PLANI
  Map...` window, `CartoImportPipeline` (also callable from editor scripts),
  `CartoSceneBuilder` demo-scene generator.
- `Tests/EditMode/` → copy to `Assets/Tests/EditMode/Carto/` — full suite with
  embedded synthetic fixtures + a machine-local Angers integration test.

## Install into a project

1. Copy the three source folders as mapped above (Unity generates `.meta` files).
2. Copy the tests; adjust their namespace (`Snake2D.Tests.Carto`) to the project's
   test namespace and the `DataDir` constant in `CartoAngersIntegrationTests.cs`
   (or delete that file if the project has no local dataset).
3. Add `"Carto.Core"` and `"Carto.Unity.Runtime"` to the EditMode test asmdef
   references.
4. Gitignore imported artifacts: `/Assets/CartoMaps/` + `/Assets/CartoMaps.meta`,
   and the source dataset folder if it lives inside the project.
5. Requirements: URP (uses the `Universal Render Pipeline/Unlit` shader),
   .NET Standard 2.1 API level (the Unity default).

## Versioning

Template sources are the canonical copy; project copies may drift. Promote to a
UPM package once the API stabilizes (planned follow-up).
```

- [ ] **Step 13.3: Commit** (PLUGIN):

```bash
git add unity-kit/templates/carto
git commit -m "feat(carto): carto template — Core/Runtime/Editor sources + tests + install README"
```

---

### Task 14: Skill `unity-carto-maps` + cross-reference

**Files:**
- Create: `unity-kit/skills/unity-carto-maps/SKILL.md`, `unity-kit/skills/unity-carto-maps/references/plani-type3.md` (PLUGIN)
- Modify: `unity-kit/skills/unity-geo-maps/SKILL.md` (PLUGIN)

- [ ] **Step 14.1: Create `unity-kit/skills/unity-carto-maps/SKILL.md`:**

```markdown
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
```

- [ ] **Step 14.2: Create `unity-kit/skills/unity-carto-maps/references/plani-type3.md`:**

```markdown
# PLANI_TYPE3 + .geo format reference

Decoded 2026-07 from `ScriptsQGIS/Convert_MP2R6.py` (producer) and real Angers
exports (`angers.xml` 102 MB, `Angers2–5.xml` 29–57 MB). All coordinates WGS84
decimal degrees (EPSG:4326); X = longitude, Y = latitude.

## Root
`<PLANI_TYPE3 LIM_EST LIM_NORD LIM_OUEST LIM_SUD NbElements="255">` — the root
NbElements is a hardcoded constant, meaningless. Children `NO/NE/SE/SO` corner
elements with `X`/`Y` attributes (the reprojected raster extent — NOT an
axis-aligned rectangle).

## Linear sections (element = attributes + `<POINTS><POINT X Y/>…</POINTS>`)
| Section | Element | Attributes |
|---|---|---|
| ROUTES | ROUTE | NOM, LONGUEUR, SITUATION, NBR_VOIES, SEPARATION, REVETEMENT, IMPORTANCE, CATEGORIE, LARGEUR_MAX, LARGEUR, MASSE_MAX, SENS |
| PONTS_LINEAIRES | PONT_LINEAIRE | NOM, LONGUEUR, HAUTEUR_DESSOUS, LARGEUR_MAX, MASSE_MAX |
| FLEUVES_LINEAIRES | FLEUVE_LINEAIRE | TYPE_FLEUVE, SENS_COURANT, VITESSE_COURANT, PROFONDEUR, LARGEUR, LONGUEUR, HAUTEUR, COMMENTAIRE, NOM |
| VOIES_FERREES | VOIE_FERREE | LARGEUR, LONGUEUR, HAUTEUR, COMMENTAIRE, NOM, SITUATION, NBRE_VOIES, TYPE_ECARTEMENT, UTILISATION, TYPE, PHYSIQUE, CLASSEMENT, ECARTEMENT |

## Zonal sections (element = attributes + `<CONTOUR><POINTS>…` outer ring + optional holes)
Holes: `<ZONES_EXCLUES><ZONE_EXCLUE><CONTOUR><POINTS>…` — one ZONE_EXCLUE per hole.
| Section | Element | Attributes |
|---|---|---|
| VEGETATIONS | VEGETATION | NOM, SURFACE, TYPE_VEGETATION, DENSITE |
| PLANS_EAU | PLAN_EAU | HAUTEUR, **COMMENAIRE (sic — producer typo)**, NOM, SURFACE |
| CONSTRUCTIONS | CONSTRUCTION | NOM, SURFACE, HAUTEUR, COMMENTAIRE (producer emits main ring only) |
| BATIMENTS | BATIMENT | NOM, SURFACE, HAUTEUR, COMMENTAIRE, NB_NIVEAUX |

## Parser leniency rules (mandatory, evidenced by real files)
- Every attribute optional → typed defaults (0 / ""); unknown attributes and
  elements skipped without error; ints may carry decimals (parse as double, truncate).
- Accept both COMMENTAIRE and COMMENAIRE on PLAN_EAU.
- Never trust NbElements (root constant 255; per-section values drift) — count
  actual elements, surface mismatches as warnings.
- Sections may be absent or empty per export: `angers.xml` has ROUTES (28 303)
  + VEGETATIONS populated but PLANS_EAU/CONSTRUCTIONS/BATIMENTS at 0;
  `Angers5.xml` has BATIMENTS populated. Handle every combination.
- Rings may repeat the first point as the last — drop the duplicate; rings with
  <3 points are dropped with a warning (feature skipped if it was the outer).
- Stream with XmlReader — files reach 102 MB, never DOM-load.
- Parse culture-invariant (French Windows locale reads "47.5" as 475 otherwise).

## .geo sidecar (georeferenced raster)
XML `<GeoReference>` next to a same-named `.gif`/`.tif`:
PixelMetreX/Y (meters per pixel), DimensionImageX/Y (pixels), Echelle
(= m/px × 10000, informational), Longitude/Latitude for NO/NE/SE/SO corners.
Angers example: 12825×12544 px at 1.5057 m/px ≈ 19.3 × 18.9 km. Unity cannot
import `.gif` — use the `.tif` sibling; imported at max texture size 8192.

## Projection (what the template bakes with)
Tangent-plane equirectangular centered on the map's LIM midpoints (lon0, lat0):
`x = (lon − lon0) · cos(lat0·π/180) · k`, `y = (lat − lat0) · k`, with
`k = π/180 × 6 378 137 m` (WGS84 semi-major axis). Deterministic double math,
float32 local-meter output; ~0.16 % E–W scale/shape distortion at the map edge
at Angers extent (rel. error ≈ tan(lat0)·Δlat). Features AND raster corners go
through the same instance, so relative layer registration is exact regardless
of absolute error.

## Upstream pipeline (provenance)
BD TOPO | VMAP1 | OSM | MGCP shapefiles → QGIS scripts (`Convert_*2MP.py`) →
modèle-pivot layers (`MP_Zonaux` polygons / `MP_Lineaires` lines / `MP_Points`;
fields NATURE, FACC, FSC, HAUTEUR, LARGEUR…) → `Convert_MP2R6.py` → PLANI_TYPE3
(+ `Creer_GifEtGeo.py` for rasters). A `DRS_MGCP2SWORD.py` exporter ties the
ecosystem to the MASA SWORD family. unity-kit consumes the XML/geo outputs and
does not reimplement the QGIS steps.

## CMAP baked binary (what the template writes)
Magic `CMAP`, ushort version (1), source name, center lon/lat (doubles), bounds
(float32), then 8 layers in fixed order (Roads, Bridges, RiverLines, Railways,
Vegetation, Water, Constructions, Buildings): int32 counts, per-feature info
fields in `FeatureInfo.cs` declaration order, geometry as int32 count +
float32 x/y pairs (local meters). Strings are BinaryWriter 7-bit-length UTF-8.
Little-endian. Loader: `CartoMapData.Load` / `CartoMapAsset.Load(TextAsset)`.
```

- [ ] **Step 14.3: Cross-reference from unity-geo-maps.** In `unity-kit/skills/unity-geo-maps/SKILL.md`, extend the final cross-refs sentence:

Old:
```
Cross-refs: unity-netcode-entities (replication), unity-dots (sim-side consumption), unity-verify.
```

New:
```
Cross-refs: unity-netcode-entities (replication), unity-dots (sim-side consumption), unity-verify, unity-carto-maps (PLANI_TYPE3 "carto" map files + .geo rasters — the SWORD-family pipeline).
```

- [ ] **Step 14.4: Commit** (PLUGIN):

```bash
git add unity-kit/skills/unity-carto-maps unity-kit/skills/unity-geo-maps/SKILL.md
git commit -m "feat(carto): unity-carto-maps skill + PLANI_TYPE3 format reference + geo-maps cross-ref"
```

---

### Task 15: Release prep (v0.8.0, no push/tag yet)

**Files:**
- Modify: `unity-kit/.claude-plugin/plugin.json`, `unity-kit/README.md`, `ROADMAP.md` (PLUGIN)

- [ ] **Step 15.1: Bump `unity-kit/.claude-plugin/plugin.json`** — change two fields (leave the rest untouched):
  - `"version": "0.6.1"` → `"version": "0.8.0"`
  - In `"description"`, replace the fragment `and orchestrate agentic gamedev workflows` with `integrate real-world PLANI_TYPE3/carto GIS maps (unity-carto-maps), and orchestrate agentic gamedev workflows`
  - In `"keywords"`, append `"gis", "maps", "carto"` before the closing bracket.

- [ ] **Step 15.2: Add the skill row to `unity-kit/README.md`.** In the skills table, insert directly after the `unity-geo-maps` row (or after `unity-dots-migration` if geo-maps has no row):

```markdown
| `unity-carto-maps` | PLANI_TYPE3 "carto"/SWORD-family GIS maps → baked Unity assets + load-time 2D map rendering (C# template: Carto.Core/Runtime/Editor) |
```

- [ ] **Step 15.3: Add a ROADMAP note.** Append at the end of `ROADMAP.md`:

```markdown

---

**v0.8.0 (in flight, branch `feat/carto-maps`):** unity-carto-maps — PLANI_TYPE3/`.geo`
map integration (skill + format reference + Carto C# template + Angers demo import).
Lands after v0.7.0 ships.
```

- [ ] **Step 15.4: Commit** (PLUGIN). Do **NOT** tag or push — the release ritual (tag `v0.8.0`, push, reinstall) is user-gated and happens after `roadmap/v0.7.0` ships:

```bash
git add unity-kit/.claude-plugin/plugin.json unity-kit/README.md ROADMAP.md
git commit -m "chore(release): v0.8.0 prep — version bump, README row, roadmap note"
```

---

### Task 16: Final acceptance check

- [ ] **Step 16.1: Run the FULL test suite** (no filter, EditMode then PlayMode) — every pre-existing snake test and every carto test green. Expected: 0 failures; the 2 integration tests pass on this machine.

- [ ] **Step 16.2: Walk the spec's §8 acceptance criteria** (spec: `docs/superpowers/specs/2026-07-24-carto-map-unity-integration-design.md`) and confirm each with evidence:
  1. Skill + reference self-sufficient → files exist in PLUGIN, reference covers schema/leniency/binary.
  2. Template compiles, zero console errors, `Carto.Core` has no `UnityEngine` refs → grep the Core folder for `UnityEngine` (expect only the asmdef flag):

```bash
grep -rn "UnityEngine" "Assets/Carto/Core" || echo "CLEAN"
```

  3. All EditMode tests green incl. Angers2 integration (Step 16.1).
  4. `CartoAngers2` scene shows raster + registered vector layers; screenshot exists in `Captures/`; scene file is KB-sized (Step 12.6).
  5. `git status` in SNAKE and PLUGIN shows no `Carto/` dataset or `Assets/CartoMaps/` files staged/tracked:

```bash
git -C "C:/Users/bencu/unityProjects/snake-unity-kit/.claude/worktrees/carto-map-unity-integration-69e523" status --short
git -C "C:/Users/bencu/claude-plugins-carto" status --short
```

  6. `feat/carto-maps` log contains spec + template + skill + release-prep commits.

- [ ] **Step 16.3: Report** — summary with test counts, import timings, screenshot path, and the two branch states. Offer the user the follow-ups: run `/unity-kit:review` (multi-lens verified review), and the v0.8.0 release ritual once v0.7.0 ships.

---

## Plan self-review notes

- **Spec coverage:** §2 decisions → Tasks 2–12 (importer, .NET Standard, data asset + visible map) and 13 (template copy-in). §3 format reference → Task 14 reference doc. §4 architecture → Tasks 1–10 (three asmdefs, no-UnityEngine Core, load-time meshes). §5 skill → Task 14. §6 verification → Tasks 11–12 + 16. §7 release mechanics → Task 15 (push/tag deliberately deferred, user-gated). §9 out-of-scope → skill "Future work" section only. No gaps found.
- **Winding is the one subtle correctness point** — encoded as tested behavior (Task 8) with the reasoning written down, not folklore.
- **Type consistency check:** `GeoArea<T>.Holes` is `List<GeoPoint[]>` (parse) vs `LocalArea<T>.Holes` is `Vector2[][]` (baked) — intentional, bake converts; tests assert `.Count` vs `.Length` accordingly. Tuple element names differ between builders and callers in places — C# tuples convert structurally, not by name.






