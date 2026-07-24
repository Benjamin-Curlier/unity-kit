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
  `CartoSceneBuilder` demo-scene generator, first-import texture preprocessor.
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
6. Note: a committed demo scene references machine-local `Assets/CartoMaps/`
   artifacts (baked bytes, materials, raster) — on a fresh clone it opens with
   missing references until a map is re-imported locally. Expected, not a bug.

## Versioning

Template sources are the canonical copy; project copies may drift. Promote to a
UPM package once the API stabilizes (planned follow-up).
