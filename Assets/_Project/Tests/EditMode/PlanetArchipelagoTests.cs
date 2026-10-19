using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class PlanetArchipelagoTests
    {
        [Test] public void EveryIslandUsesAFullHeightFieldAndTheSameSurfaceSamples()
        {
            var field=IslandHeightField.Build(new IslandSettings(),17);
            var planet=new PlanetModel(3000,0.7f,5,17,190);
            planet.BuildArchipelago(field,17);
            Assert.That(planet.PatchCount,Is.EqualTo(5));
            Assert.That(planet.GetPatch(0).Field,Is.SameAs(field));
            for(int i=0;i<planet.PatchCount;i++)
            {
                var patch=planet.GetPatch(i);
                Assert.That(patch.Field.Lakes.Count,Is.GreaterThan(0));
                Assert.That(patch.Field.Rivers.Count,Is.GreaterThan(0));
                Assert.That(patch.Field.Caves.Count,Is.GreaterThan(0));
                var hub=patch.Field.HubCenter;
                var dir=patch.DirectionAt(hub.x,hub.z);
                Assert.That(planet.Sample(dir).GroundHeight,Is.EqualTo(patch.Field.SampleHeight(hub.x,hub.z)).Within(0.03f));
                Assert.That(patch.WeightAt(dir),Is.EqualTo(1));
            }
        }

        [TestCase(1,0,0)] [TestCase(0,-1,0)] [TestCase(0,0,-1)]
        public void IslandChartRoundTripsPositionsAndOrientationOnEitherSide(int x,int y,int z)
        {
            var field=IslandHeightField.Build(new IslandSettings(),3);
            var patch=new PlanetIslandPatch(field,new Vector3(x,y,z),3000,4);
            var center=new Vector3(100,-3000,70);
            foreach(var at in new[]{Vector3.zero,new Vector3(33,12,-47),new Vector3(-120,-4,83)})
            {
                var world=patch.ToWorld(at,center);
                Assert.That(Vector3.Distance(patch.ToField(world,center),at),Is.LessThan(0.003f));
                var radial=(world-center).normalized;
                Assert.That(Vector3.Dot(patch.RotationAt(at)*Vector3.up,radial),Is.GreaterThan(0.9999f));
            }
        }

        [Test] public void LakesKeepTheirElevatedWaterDatumAndCavesKeepTheirFloor()
        {
            var field=IslandHeightField.Build(new IslandSettings(),17);
            var planet=new PlanetModel(3000,0.7f,3,17,190);
            planet.BuildArchipelago(field,17);
            foreach(var lake in field.Lakes)
            {
                var dir=planet.GetPatch(0).DirectionAt(lake.Center.x,lake.Center.z);
                float water=field.SampleWaterSurface(lake.Center.x,lake.Center.z);
                Assert.That(planet.Sample(dir).WaterHeight,Is.EqualTo(water).Within(0.03f));
            }
            foreach(var cave in field.Caves)
            {
                var dir=planet.GetPatch(0).DirectionAt(cave.Position.x,cave.Position.z);
                Assert.That(planet.Sample(dir).GroundHeight,Is.EqualTo(field.SampleHeight(cave.Position.x,cave.Position.z)).Within(0.03f));
            }
        }
    }
}
