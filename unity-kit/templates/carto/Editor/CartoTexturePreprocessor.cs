using UnityEditor;

namespace Carto.Unity.Editor
{
    /// <summary>
    /// First-import settings for rasters copied into Assets/CartoMaps — avoids the
    /// default-2048 decode followed by a second full 8192 decode of a 161 MP TIF.
    /// </summary>
    sealed class CartoTexturePreprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CartoImportPipeline.DefaultOutputFolder + "/")) return;
            var ti = (TextureImporter)assetImporter;
            if (ti.maxTextureSize < 8192) ti.maxTextureSize = 8192;
        }
    }
}
