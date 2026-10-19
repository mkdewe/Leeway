using System.Collections;
using Leeway.World;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    public class PlanetStartupColourPlayTests
    {
        private GameObject _root;
        private FishNet.Managing.NetworkManager _network;
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(_root)Object.Destroy(_root);
            if(_network!=null){_network.ClientManager.StopConnection();_network.ServerManager.StopConnection(true);}
            yield return null;
        }


        [UnityTest] public IEnumerator WorldPublicationWaitsForFinishedTerrainAndCookedCollision()
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            var planet=Object.FindFirstObjectByType<Leeway.Planet.PlanetView>(FindObjectsInactive.Include);
            var seed=Object.FindFirstObjectByType<IslandSeed>(FindObjectsInactive.Include);
            typeof(IslandSeed).GetField("_fixedSeed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(seed,1);
            _network=Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            _network.ServerManager.StartConnection();_network.ClientManager.StartConnection();
            float end=Time.realtimeSinceStartup+25;
            while(!planet.PlayableWorld.IsReady)
            {
                Assert.That(planet.IsVisible,Is.False,"No coarse substitute may be shown as a finished world.");
                Assert.That(Leeway.Gravity.GravityWorld.IsSceneReady(planet.gameObject.scene),Is.False,
                    "Creatures must not be admitted before the surface is presentable.");
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(end),"World preparation must finish within the existing startup timeout.");
                yield return null;
            }
            Assert.That(planet.SurfacePrepared,Is.True);
            Assert.That(planet.IsVisible,Is.True,"The ready event publishes the terrain atomically.");
            Assert.That(Camera.main.cullingMask,Is.Not.EqualTo(0));
            Assert.That(planet.PlayableWorld.Collisions.ReadyAt(seed.Generator.HubPosition),Is.True);
            Assert.That(planet.StableGroundReadyAt(seed.Generator.HubPosition),Is.True);
        }

        [UnityTest] public IEnumerator TemporaryFlatWorldNeverAppearsBeforeThePlayableSphere()
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            var source=Object.FindFirstObjectByType<IslandSeed>(FindObjectsInactive.Include).Generator;
            Assert.That(source.PlanetSurfaceOwnsGround,Is.True,"The real scene must assign terrain ownership before the host generates its field.");
            _root=new GameObject("Planet startup colour regression");
            var generator=_root.AddComponent<IslandGenerator>();
            typeof(IslandGenerator).GetField("_biome",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .SetValue(generator,source.Biome);
            generator.PlanetSurfaceOwnsGround=true;
            generator.Generate(1);
            Assert.That(generator.IsGenerated,Is.True);
            Assert.That(generator.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(0),
                "The temporary flat world must never be rendered, even for one frame.");
            yield return null;
            Assert.That(generator.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(0));
            var patch=new PlanetIslandPatch(generator.Field,Vector3.up,3500);
            yield return generator.GenerateOnPlanetAsync(patch,new Vector3(0,-3500,0));
            Assert.That(generator.GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0),
                "The projected details become visible once they belong to the spherical world.");
            var terrain=_root.transform.Find("GeneratedIsland/Terrain");
            Assert.That(terrain.gameObject.activeSelf,Is.False,"The flat terrain stays hidden after activation.");
        }
    }
}
