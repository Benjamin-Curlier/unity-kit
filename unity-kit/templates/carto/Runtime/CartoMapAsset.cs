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
