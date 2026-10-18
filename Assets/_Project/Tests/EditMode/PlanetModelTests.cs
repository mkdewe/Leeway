using Leeway.Planet;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class PlanetModelTests
    {
        private const float Home = 190f;

        private static PlanetModel Make(float radius = 3500f, float water = 0.7f, int islands = 9, int seed = 5) =>
            new(radius, water, islands, seed, Home);

        // The share of the sphere that is land, counted on an even spread of points.
        private static float MeasuredLand(PlanetModel model)
        {
            const int count = 4000;
            int land = 0;
            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / count;
                float ring = Mathf.Sqrt(1f - y * y);
                float angle = i * 2.39996323f;
                if (model.Sample(new Vector3(ring * Mathf.Cos(angle), y, ring * Mathf.Sin(angle))).IsLand) land++;
            }
            return land / (float)count;
        }

        [Test]
        public void SameNumbers_GiveTheSamePlanet()
        {
            PlanetModel a = Make(), b = Make();
            for (int i = 0; i < a.IslandCount; i++)
                Assert.That(a.GetIsland(i).Direction, Is.EqualTo(b.GetIsland(i).Direction));

            Vector3 probe = new Vector3(0.3f, 0.5f, -0.8f).normalized;
            Assert.That(a.Sample(probe).Height, Is.EqualTo(b.Sample(probe).Height));
        }

        [Test]
        public void ADifferentSeed_MovesTheIslands()
        {
            Assert.That(Make(seed: 5).GetIsland(1).Direction, Is.Not.EqualTo(Make(seed: 6).GetIsland(1).Direction));
        }

        [Test]
        public void TheHomeIslandIsAtTheNorthPoleAndIsLand()
        {
            PlanetModel model = Make();
            Assert.That(model.GetIsland(0).IsHome, Is.True);
            Assert.That(model.GetIsland(0).Direction, Is.EqualTo(Vector3.up));
            Assert.That(model.Sample(Vector3.up).IsLand, Is.True);
        }

        [TestCase(0.5f)]
        [TestCase(0.7f)]
        [TestCase(0.85f)]
        public void TheLandMatchesTheRequestedShareOfSea(float water)
        {
            PlanetModel model = Make(water: water, islands: 12);
            Assert.That(MeasuredLand(model), Is.EqualTo(1f - water).Within(0.05f), "measured land share");
            Assert.That(model.LandFraction, Is.EqualTo(1f - water).Within(0.04f), "land share the model reports");
        }

        [Test]
        public void MoreIslandsAreMoreIslandsNotMoreLand()
        {
            PlanetModel few = Make(islands: 3), many = Make(islands: 25);
            Assert.That(many.IslandCount, Is.EqualTo(25));
            Assert.That(many.LandFraction, Is.EqualTo(few.LandFraction).Within(0.05f));
            Assert.That(many.GetIsland(5).AngularRadius, Is.LessThan(few.GetIsland(1).AngularRadius));
        }

        [Test]
        public void ABiggerPlanetShrinksTheHomeIslandOnIt()
        {
            Assert.That(Make(radius: 9000f).GetIsland(0).AngularRadius, Is.LessThan(Make(radius: 2000f).GetIsland(0).AngularRadius));
        }

        [Test]
        public void SeaIsFlatAndLandIsAboveIt()
        {
            PlanetModel model = Make();
            bool sawSea = false, sawLand = false;
            for (int i = 0; i < 400; i++)
            {
                PlanetModel.Point p = model.Sample(Random.onUnitSphere);
                if (p.IsLand) { sawLand = true; Assert.That(p.Height, Is.GreaterThanOrEqualTo(0f)); }
                else { sawSea = true; Assert.That(p.Height, Is.EqualTo(0f)); Assert.That(p.Depth, Is.InRange(0f, 1f)); }
            }
            Assert.That(sawSea && sawLand, Is.True);
        }

        [Test]
        public void ASingleIslandPlanetBuilds()
        {
            PlanetModel model = Make(islands: 1);
            Assert.That(model.IslandCount, Is.EqualTo(1));
            Assert.That(model.Sample(Vector3.down).IsLand, Is.False);
        }

        [Test]
        public void ATileStaysOnTheSphereAndFacesOut()
        {
            PlanetModel model = Make();
            PlanetTerrain.Data tile = PlanetTerrain.Build(model, 2, 3, 2, 5);

            int side = PlanetTerrain.Grid + 1;
            Assert.That(tile.Vertices.Length, Is.EqualTo(side * side + 4 * side), "the grid and four skirts");
            for (int i = 0; i < side * side; i++)
            {
                Vector3 world = tile.Vertices[i].Position + tile.Origin;
                Assert.That(world.magnitude, Is.InRange(model.Radius - 0.01f, model.Radius + model.MaxHeight + 0.01f));
                Assert.That(Vector3.Dot(tile.Vertices[i].Normal, world.normalized), Is.GreaterThan(0.2f), "the ground faces out");
            }

            // The skirt hangs below the edge it belongs to.
            for (int i = side * side; i < tile.Vertices.Length; i++)
                Assert.That((tile.Vertices[i].Position + tile.Origin).magnitude, Is.LessThan(model.Radius + model.MaxHeight));
        }

        [Test]
        public void NeighbouringTilesMeetOnTheirSharedEdge()
        {
            PlanetModel model = Make();
            PlanetTerrain.Data left = PlanetTerrain.Build(model, 4, 3, 3, 4);
            PlanetTerrain.Data right = PlanetTerrain.Build(model, 4, 3, 4, 4);

            int side = PlanetTerrain.Grid + 1;
            for (int t = 0; t < side; t++)
            {
                Vector3 a = left.Vertices[t * side + PlanetTerrain.Grid].Position + left.Origin;
                Vector3 b = right.Vertices[t * side].Position + right.Origin;
                Assert.That((a - b).magnitude, Is.LessThan(0.01f), "no crack between tiles of one level");
                Assert.That(Vector3.Dot(left.Vertices[t * side + PlanetTerrain.Grid].Normal, right.Vertices[t * side].Normal), Is.GreaterThan(0.999f), "no seam in the lighting");
            }
        }

        [Test]
        public void TheTrianglesOfATileFaceOutwards()
        {
            PlanetModel model = Make();
            PlanetTerrain.Data tile = PlanetTerrain.Build(model, 0, 3, 1, 1);
            ushort[] indices = PlanetTerrain.Indices();

            int grid = PlanetTerrain.Grid * PlanetTerrain.Grid * 6;
            for (int i = 0; i < grid; i += 3 * 17)
            {
                Vector3 a = tile.Vertices[indices[i]].Position, b = tile.Vertices[indices[i + 1]].Position, c = tile.Vertices[indices[i + 2]].Position;
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), a + tile.Origin), Is.GreaterThan(0f), "winding at index " + i);
            }
        }

        [Test]
        public void TheRealHomeIslandReplacesThePlanetsOwnNearThePole()
        {
            PlanetModel model = Make();
            model.SetHome((x, z) => 12f, 400f);

            Assert.That(model.Sample(Vector3.up).Height, Is.EqualTo(12f).Within(0.01f));
            Assert.That(model.Sample(Vector3.up).IsLand, Is.True);

            // Far from the pole nothing has changed.
            Vector3 far = new Vector3(0.7f, 0.3f, 0.65f).normalized;
            PlanetModel plain = Make();
            Assert.That(model.Sample(far).Height, Is.EqualTo(plain.Sample(far).Height).Within(1e-4f));
        }
    }
}
