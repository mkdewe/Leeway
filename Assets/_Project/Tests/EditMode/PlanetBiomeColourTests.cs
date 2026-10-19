using Leeway.Planet;
using Leeway.World;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class PlanetBiomeColourTests
    {
        private IslandBiome _biome;
        private IslandHeightField _field;
        private PlanetModel _model;
        [SetUp] public void SetUp()
        {
            _biome=ScriptableObject.CreateInstance<IslandBiome>();
            _field=IslandHeightField.Build(_biome.Shape,1);
            _model=new PlanetModel(3500,0.7f,1,1,190);
            _model.BuildArchipelago(_field,1);
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(_biome);}

        [Test] public void PlanetGrassKeepsTheIslandBiomeInsteadOfSwitchingToBrightGreen()
        {
            var chart=_field.HubCenter;
            var direction=_model.GetPatch(0).DirectionAt(chart.x,chart.z);
            Color actual=PlanetPalette.ForBiome(_model,_biome)(direction,_model.Sample(direction));
            Assert.That(actual.g,Is.InRange(_biome.GrassDark.g*0.96f,_biome.Grass.g*1.04f),
                "The playable island must keep its authored grey-green grass, including from space.");
            var palette=new IslandGroundPalette(_biome);
            Color expected=palette.At(_field,new IslandNoise(_field.Seed+101),
                new Vector3(chart.x,_field.SampleHeight(chart.x,chart.z),chart.z),_field.SampleNormal(chart.x,chart.z));
            Assert.That(Vector3.Distance(new Vector3(actual.r,actual.g,actual.b),new Vector3(expected.r,expected.g,expected.b)),
                Is.LessThan(0.012f));
        }

        [Test] public void CoarseAndFineTilesUseTheSameBiomeColourAtTheirSharedVertices()
        {
            var sample=PlanetPalette.ForBiome(_model,_biome);
            var coarse=PlanetTerrain.Build(_model,2,5,16,16,colour:sample);
            var fine=PlanetTerrain.Build(_model,2,6,32,32,colour:sample);
            int side=PlanetTerrain.Grid+1;
            for(int y=0;y<=PlanetTerrain.Grid;y+=2)
            for(int x=0;x<=PlanetTerrain.Grid;x+=2)
            {
                var a=coarse.Vertices[(y/2)*side+x/2];
                var b=fine.Vertices[y*side+x];
                Assert.That(Vector3.Distance(a.Position+coarse.Origin,b.Position+fine.Origin),Is.LessThan(0.002f));
                Assert.That(Mathf.Abs(a.Colour.r-b.Colour.r),Is.LessThanOrEqualTo(1));
                Assert.That(Mathf.Abs(a.Colour.g-b.Colour.g),Is.LessThanOrEqualTo(1));
                Assert.That(Mathf.Abs(a.Colour.b-b.Colour.b),Is.LessThanOrEqualTo(1));
            }
        }
    }
}
