using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class StarSystemModelTests
    {
        [Test]
        public void HasTheRequestedNumberOfPlanetsAndMoons()
        {
            var system = new StarSystemModel(6, 3, 11);

            Assert.That(system.Planets.Count, Is.EqualTo(6));
            Assert.That(system.Moons.Count, Is.EqualTo(3));
        }

        [Test]
        public void TheHomePlanetIsOneAuOutAndOnlyOneIsHome()
        {
            var system = new StarSystemModel(5, 1, 3);

            Assert.That(system.Planets[system.HomeIndex].OrbitAu, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(system.Planets[system.HomeIndex].IsHome, Is.True);
            int homes = 0;
            foreach (StarSystemModel.Planet planet in system.Planets) if (planet.IsHome) homes++;
            Assert.That(homes, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(8)]
        public void AnyNumberOfPlanetsHasAHome(int count)
        {
            var system = new StarSystemModel(count, 0, 5);

            Assert.That(system.HomeIndex, Is.InRange(0, count - 1));
            Assert.That(system.Moons.Count, Is.EqualTo(0));
        }

        [Test]
        public void OrbitsGrowOutwardAndOuterPlanetsAreSlower()
        {
            var system = new StarSystemModel(7, 0, 9);

            for (int i = 1; i < system.Planets.Count; i++)
            {
                Assert.That(system.Planets[i].OrbitAu, Is.GreaterThan(system.Planets[i - 1].OrbitAu));
                Assert.That(system.Planets[i].PeriodSeconds, Is.GreaterThan(system.Planets[i - 1].PeriodSeconds));
            }
        }

        [Test]
        public void PlanetsStayOnTheirOrbitAndComeRoundAfterOnePeriod()
        {
            var system = new StarSystemModel(4, 0, 2);
            StarSystemModel.Planet planet = system.Planets[1];

            Vector3 start = system.PlanetPosition(1, 10.0);
            Vector3 later = system.PlanetPosition(1, 10.0 + planet.PeriodSeconds * 0.37);
            Vector3 round = system.PlanetPosition(1, 10.0 + planet.PeriodSeconds);

            Assert.That(start.magnitude, Is.EqualTo(planet.OrbitAu).Within(1e-3f));
            Assert.That(later.magnitude, Is.EqualTo(planet.OrbitAu).Within(1e-3f));
            Assert.That((round - start).magnitude, Is.LessThan(1e-3f));
            Assert.That((later - start).magnitude, Is.GreaterThan(0.01f), "it moves");
        }

        [Test]
        public void MoonsKeepTheirDistanceFromThePlanet()
        {
            var system = new StarSystemModel(5, 2, 4);

            for (int i = 0; i < 2; i++)
                foreach (double t in new[] { 0.0, 33.0, 120.5 })
                    Assert.That(system.MoonOffset(i, t).magnitude, Is.EqualTo(system.Moons[i].OrbitRadius).Within(1e-3f));
        }

        [Test]
        public void TheSameNumbersMakeTheSameSystem()
        {
            var a = new StarSystemModel(5, 2, 77);
            var b = new StarSystemModel(5, 2, 77);
            var c = new StarSystemModel(5, 2, 78);

            Assert.That(a.PlanetPosition(3, 50.0), Is.EqualTo(b.PlanetPosition(3, 50.0)));
            Assert.That(a.MoonOffset(1, 50.0), Is.EqualTo(b.MoonOffset(1, 50.0)));
            Assert.That(a.PlanetPosition(3, 50.0), Is.Not.EqualTo(c.PlanetPosition(3, 50.0)));
        }

        [Test]
        public void MoonsAreSmallerThanThePlanetTheyGoRound()
        {
            var system = new StarSystemModel(3, 6, 8);

            foreach (StarSystemModel.Moon moon in system.Moons)
            {
                Assert.That(moon.Size, Is.LessThan(0.5f));
                Assert.That(moon.OrbitRadius, Is.GreaterThan(2f), "outside the planet");
            }
        }
    }
}
