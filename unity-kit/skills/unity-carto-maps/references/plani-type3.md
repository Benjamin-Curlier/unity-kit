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
