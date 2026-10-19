using System.Collections;
using System.Reflection;
using Leeway.Planet;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    public class PlanetTerrainMemoryPlayTests
    {
        [UnityTest] public IEnumerator LandStreamingStaysWithinItsMemoryBudget() => CheckBudget(false);
        [UnityTest] public IEnumerator WaterStreamingStaysWithinItsMemoryBudget() => CheckBudget(true);

        private static IEnumerator CheckBudget(bool water)
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            var view=Object.FindFirstObjectByType<PlanetView>(FindObjectsInactive.Include);
            var material=(Material)typeof(PlanetView).GetField("_material",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            var root=new GameObject("Bounded terrain regression");
            var cameraObject=new GameObject("Streaming test camera");
            cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var model=new PlanetModel(3500,0.7f,9,5,190);
            var terrain=new PlanetTerrain(model,root.transform,material,water)
            { MaxResidentTiles=128, LimitLevel=PlanetTerrain.MaxLevel };
            bool built=false;
            try
            {
                for(int tick=0;tick<240;tick++)
                {
                    int phase=(tick/40)%3;
                    Vector3 direction=phase==2?Vector3.right:Vector3.up;
                    float altitude=phase==1?model.Radius*2:model.Sample(direction).GroundHeight+8;
                    camera.transform.position=direction*(model.Radius+altitude);
                    camera.transform.LookAt(direction*model.Radius,phase==2?Vector3.up:Vector3.forward);
                    terrain.Update(camera,true);
                    built|=terrain.CoarseReady;
                    Assert.That(terrain.ResidentTiles+terrain.PendingTiles,Is.LessThanOrEqualTo(128));
                    Assert.That(terrain.PooledMeshes,Is.LessThanOrEqualTo(32));
                    Assert.That(terrain.PooledObjects,Is.LessThanOrEqualTo(32));
                    yield return null;
                }
                Assert.That(built,Is.True,"The budget must still allow a complete coarse planet.");
                Assert.That(terrain.ShownTiles,Is.GreaterThan(0),"Memory pressure must preserve visible fallback terrain.");
            }
            finally { terrain.Dispose(); Object.DestroyImmediate(root); }
            yield return null;
        }
    }
}
