using Leeway.World.Domain;
using Leeway.Planet;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class PlanetCollisionGeometryTests
    {
        [Test] public void CubeTileAddressesRoundTripAcrossEveryFace()
        {
            for(int face=0;face<6;face++)
            for(int x=0;x<8;x++)
            for(int y=0;y<8;y++)
            {
                var key=new PlanetTileKey(face,3,x,y);
                Assert.That(PlanetTileKey.At(key.CenterDirection,3),Is.EqualTo(key));
            }
        }

        [Test] public void AdjacentFaceEdgesGiveTheSameWorldPoint()
        {
            for(int i=0;i<=16;i++)
            {
                float v=-1+2f*i/16;
                Vector3 a=PlanetTileKey.Direction(0,-1,v);
                Vector3 b=PlanetTileKey.Direction(4,1,v);
                Assert.That(Vector3.Distance(a,b),Is.LessThan(1e-6f));
            }
        }

        [Test] public void PhysicalOceanFloorIsBelowTheWaterEnvelope()
        {
            var model=new PlanetModel(3000,0.8f,9,12,190);
            bool ocean=false;
            for(int i=0;i<100;i++)
            {
                var dir=PlanetTileKey.Direction(i%6,-0.9f+(i%10)*0.18f,0.3f);
                var sample=model.Sample(dir);
                if(sample.IsLand)continue;
                ocean=true;
                Assert.That(sample.Height,Is.EqualTo(0));
                Assert.That(sample.GroundHeight,Is.LessThanOrEqualTo(0));
                Assert.That(sample.GroundHeight,Is.EqualTo(-45f*sample.Depth).Within(0.001f));
            }
            Assert.That(ocean,Is.True);
        }

        [Test] public void HomeUnderwaterGroundIsNotClampedToSeaLevel()
        {
            var model=new PlanetModel(3000,0.7f,9,5,190);
            model.SetHome((x,z)=>-4.25f,400);
            Assert.That(model.Sample(Vector3.up).GroundHeight,Is.EqualTo(-4.25f).Within(0.001f));
            Assert.That(model.Sample(Vector3.up).Height,Is.EqualTo(0));
        }
    }
}
