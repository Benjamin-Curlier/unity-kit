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
