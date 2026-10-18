using System.Collections;
using System.Linq;
using FishNet.Managing;
using Leeway.Planet;
using Leeway.World;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>The planet in the running game: built on demand, flown to and from, and editable by the host.</summary>
    public class PlanetPlayTests
    {
        private NetworkManager _network;
        private PlanetView _planet;
        private PlanetCameraDirector _director;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Application.runInBackground = true;
            yield return CreatureEditorTestScene.EnsureLoaded();

            _network = Object.FindFirstObjectByType<NetworkManager>();
            var islandSeed = Object.FindFirstObjectByType<IslandSeed>(FindObjectsInactive.Include);
            typeof(IslandSeed).GetField("_fixedSeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(islandSeed, 1);

            _network.ServerManager.StartConnection();
            _network.ClientManager.StartConnection();
            yield return Until(() => _network.IsServerStarted && _network.IsClientStarted && islandSeed.Seed != 0, "host and island seed");
            yield return Until(() => islandSeed.Generator.IsGenerated, "island");

            _planet = Object.FindFirstObjectByType<PlanetView>();
            _director = Object.FindFirstObjectByType<PlanetCameraDirector>();
            Assert.That(_planet, Is.Not.Null, "The scene has the planet.");
            Assert.That(_director, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The scene is shared with other tests: the camera comes back down and the island is drawn again.
            if (_director) _director.ExitPlanetView();
            if (_director) yield return Until(() => !_director.IsAway, "camera back on the ground");

            if (_network)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return Until(() => !_network.IsServerStarted && !_network.IsClientStarted, "host stop");
            }

            yield return null;
        }

        private static bool FlatWorldShown() =>
            Object.FindFirstObjectByType<IslandGenerator>().GetComponentsInChildren<Renderer>().Any(r => r.enabled);

        [UnityTest]
        public IEnumerator ScrollingOutLeavesForThePlanetAndComesBack()
        {
            Assert.That(FlatWorldShown(), Is.True, "The island is drawn before the flight.");

            _director.EnterPlanetView();
            Assert.That(_director.InPlanetView, Is.True);
            Assert.That(_planet.IsVisible, Is.True);
            yield return Until(() => _planet.IsBuilt, "planet built in the background");

            var planetCamera = _director.GetComponentInChildren<CinemachineCamera>();
            yield return Until(() => CinemachineCore.IsLive(planetCamera), "planet camera live");
            yield return Until(() => !FlatWorldShown(), "flat world hidden once the camera has arrived");

            // The camera looks at the planet from outside it.
            float distance = (Camera.main.transform.position - _planet.Center).magnitude;
            Assert.That(distance, Is.GreaterThan(_planet.Radius), "outside the planet");

            _director.ExitPlanetView();
            yield return Until(() => FlatWorldShown(), "the island to be shown again as the camera heads back");
            yield return Until(() => !_director.IsAway, "the camera back on the ground");
            yield return Until(() => !_planet.IsVisible, "planet hidden after the descent");
            Assert.That(_director.InPlanetView, Is.False);
        }

        [UnityTest]
        public IEnumerator TheHostCanReshapeThePlanet()
        {
            _director.EnterPlanetView();
            Assert.That(_planet.CanEdit, Is.True);

            Assert.That(_planet.Apply(5200f, 0.5f, 20), Is.True);
            yield return Until(() => _planet.IsBuilt && _planet.Model.IslandCount == 20, "rebuilt with 20 islands");

            Assert.That(_planet.Model.Radius, Is.EqualTo(5200f).Within(0.1f));
            Assert.That(_planet.Model.LandFraction, Is.EqualTo(0.5f).Within(0.05f));

            // Out-of-range numbers are held to the limits.
            _planet.Apply(999999f, 2f, 500);
            Assert.That(_planet.Current().Radius, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxRadius));
            Assert.That(_planet.Current().IslandCount, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxIslands));
        }

        [UnityTest]
        public IEnumerator LeavingTheAirShowsTheStarSystemAndComingBackHidesIt()
        {
            var backdrop = Object.FindFirstObjectByType<SpaceBackdrop>();
            Assert.That(backdrop, Is.Not.Null, "The scene has the star system.");
            yield return Until(() => backdrop.Model != null, "the star system");
            Transform system = backdrop.transform.Find("StarSystem");
            Assert.That(system.gameObject.activeSelf, Is.False, "Nothing of space is drawn from the ground.");
            Assert.That(_director.Space, Is.EqualTo(0f).Within(0.01f));

            _director.EnterPlanetView();
            yield return Until(() => _director.Space >= 0.99f, "the camera to be out of the air");
            yield return Until(() => system.gameObject.activeSelf, "the star system to be drawn");
            Assert.That(Shader.GetGlobalFloat("_LeewaySpace"), Is.EqualTo(1f).Within(0.02f), "The sky goes to stars.");

            _director.ExitPlanetView();
            yield return Until(() => !_director.IsAway, "the camera back on the ground");
            yield return null;
            Assert.That(_director.Space, Is.EqualTo(0f).Within(0.01f));
            Assert.That(system.gameObject.activeSelf, Is.False);
            Assert.That(Shader.GetGlobalFloat("_LeewaySpace"), Is.EqualTo(0f).Within(0.01f), "The air is back.");
        }

        [UnityTest]
        public IEnumerator TheClimbRisesStraightUpBeforeItSwingsRoundAndTheGroundGetsFinerAsItIsApproached()
        {
            yield return Until(() => _planet.IsBuilt, "the planet");
            _director.EnterPlanetView();

            float widest = 0f;
            int tilesAtStart = 0;
            float end = Time.realtimeSinceStartup + 10f;
            while (_director.Space < 0.2f && Time.realtimeSinceStartup < end)
            {
                Vector3 above = Camera.main.transform.position - _planet.Anchor;
                if (above.y > 100f) widest = Mathf.Max(widest, new Vector2(above.x, above.z).magnitude / above.y);
                tilesAtStart = Mathf.Max(tilesAtStart, _planet.ShownTiles);
                yield return null;
            }
            Assert.That(_director.Space, Is.GreaterThanOrEqualTo(0.2f), "The camera left the ground.");
            Assert.That(widest, Is.LessThan(0.15f), "Through the lower air the camera goes up, not along.");

            yield return Until(() => _planet.ShownTiles > 100, "the fine tiles of the planet");
            Assert.That(_planet.ResidentTiles, Is.GreaterThan(96), "Tiles finer than the coarsest are made as the camera comes near.");
        }

        [UnityTest]
        public IEnumerator TheHostCanChangeTheStarSystem()
        {
            var backdrop = Object.FindFirstObjectByType<SpaceBackdrop>();
            yield return Until(() => backdrop.Model != null, "the star system");

            Assert.That(_planet.ApplySystem(7, 3), Is.True);
            yield return Until(() => backdrop.Model.Planets.Count == 7 && backdrop.Model.Moons.Count == 3, "the system to be rebuilt");

            _planet.ApplySystem(999, 999);
            Assert.That(_planet.Current().PlanetCount, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxPlanets));
            Assert.That(_planet.Current().MoonCount, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxMoons));
        }

        private static IEnumerator Until(System.Func<bool> condition, string what, float timeout = 25f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }
    }
}
