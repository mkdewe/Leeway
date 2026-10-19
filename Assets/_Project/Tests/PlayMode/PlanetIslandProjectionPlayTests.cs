using System.Collections;
using Leeway.Planet;
using Leeway.World;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    public class PlanetIslandProjectionPlayTests
    {
        private GameObject _root;
        [UnityTearDown] public IEnumerator TearDown(){if(_root)Object.Destroy(_root);yield return null;}

        [UnityTest] public IEnumerator FullIslandKeepsWaterCavesAndSceneryOnSphere()
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            var source=Object.FindFirstObjectByType<IslandGenerator>(FindObjectsInactive.Include);
            Assert.That(source.Biome,Is.Not.Null);
            var field=IslandHeightField.Build(source.Biome.Shape,1);
            var patch=new PlanetIslandPatch(field,Vector3.right,3000);
            var center=new Vector3(15000,-3000,0);
            _root=new GameObject("Projected island regression");
            var generator=_root.AddComponent<IslandGenerator>();
            generator.GenerateOnPlanet(patch,center,source.Biome);
            Assert.That(generator.Field,Is.SameAs(field));
            Assert.That(field.Lakes.Count,Is.GreaterThan(0));
            Assert.That(field.Rivers.Count,Is.GreaterThan(0));
            Assert.That(field.Caves.Count,Is.GreaterThan(0));
            Assert.That(Vector3.Distance(generator.WorldToField(generator.HubPosition),field.HubCenter),Is.LessThan(0.01f));
            var water=_root.GetComponentsInChildren<MeshFilter>(true);
            bool sawWater=false,sawCaves=false;
            foreach(var filter in water)
            {
                if(filter.name=="Water")
                {
                    sawWater=true;
                    var colours=filter.sharedMesh.colors;
                    var triangles=filter.sharedMesh.triangles;
                    Assert.That(triangles.Length,Is.GreaterThan(0),"Lakes and rivers remain rendered.");
                    for(int i=0;i<triangles.Length;i+=3)
                        Assert.That(Mathf.Max(colours[triangles[i]].g,Mathf.Max(colours[triangles[i+1]].g,colours[triangles[i+2]].g)),
                            Is.GreaterThan(0.5f),"Island details must not draw a second ocean over streamed sea tiles.");
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 world=filter.transform.TransformPoint(vertex);
                        Vector3 chart=patch.ToField(world,center);
                        float expected=field.SampleWaterSurface(chart.x,chart.z);
                        Assert.That(chart.y,Is.EqualTo(expected+0.008f).Within(0.08f));
                    }
                }
                for(var parent=filter.transform;parent!=null;parent=parent.parent)
                    if(parent.name=="Caves")sawCaves=true;
            }
            Assert.That(sawWater,Is.True);
            foreach(var node in _root.GetComponentsInChildren<Transform>(true))
                if(node.name=="Caves"&&node.GetComponentsInChildren<Collider>(true).Length>0)sawCaves=true;
            Assert.That(sawCaves,Is.True,"Caves retain their collision proxies; their render meshes are batched with scenery.");
            Assert.That(_root.GetComponentsInChildren<Collider>(true).Length,Is.GreaterThan(0));
            yield return null;
        }
    }
}
