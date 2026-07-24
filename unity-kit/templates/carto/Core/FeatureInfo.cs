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
