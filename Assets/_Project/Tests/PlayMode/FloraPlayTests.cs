using System.Collections;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using Leeway.Combat;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.World;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>The edible flora in the running game: it grows on the island, feeds a creature and grows back.</summary>
    public class FloraPlayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private NetworkManager _network;
        private PlaygroundCreatureController _player;
        private IslandFlora _flora;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Application.runInBackground = true;
            yield return CreatureEditorTestScene.EnsureLoaded();

            _network = Object.FindFirstObjectByType<NetworkManager>();
            var islandSeed = Object.FindFirstObjectByType<IslandSeed>(FindObjectsInactive.Include);
            typeof(IslandSeed).GetField("_fixedSeed", Private).SetValue(islandSeed, 1);

            _network.ServerManager.StartConnection();
            _network.ClientManager.StartConnection();
            yield return Until(() => _network.IsServerStarted && _network.IsClientStarted, "host");
            yield return Until(() => Object.FindObjectsByType<PlaygroundCreatureController>(FindObjectsSortMode.None).Any(b => b.IsOwner), "player body");

            _player = Object.FindObjectsByType<PlaygroundCreatureController>(FindObjectsSortMode.None).First(b => b.IsOwner);
            _flora = Object.FindFirstObjectByType<IslandFlora>();
            Assert.That(_flora, Is.Not.Null, "The scene has the flora.");
            yield return Until(() => _flora.PlantCount > 20, "plants grown");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_network)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return Until(() => !_network.IsServerStarted && !_network.IsClientStarted, "host stop");
            }

            yield return null;
        }

        private void StandBeside(int plant)
        {
            Assert.That(_flora.TryGetPlant(plant, out Vector3 position, out _), Is.True);

            var rb = _player.GetComponent<Rigidbody>();
            Vector3 spot = position + Vector3.right * 0.8f + Vector3.up * 0.8f;
            rb.position = spot;
            _player.transform.position = spot;
            rb.linearVelocity = Vector3.zero;
            _player.GetComponent<CreaturePuppet>()?.Teleport();
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator EveryKindOfPlantGrowsOnTheIsland()
        {
            var kinds = Enumerable.Range(0, _flora.PlantCount)
                .Select(i => { _flora.TryGetPlant(i, out _, out FloraKind kind); return kind; })
                .Distinct()
                .ToList();

            Assert.That(kinds, Does.Contain(FloraKind.BerryBush));
            Assert.That(kinds, Does.Contain(FloraKind.FruitTree));
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(4), "Mushrooms, herbs and poisonous caps grow here too.");
            yield break;
        }

        [UnityTest]
        public IEnumerator EatingFeedsTheCreatureAndStripsThePlantUntilItGrowsBack()
        {
            int index = Enumerable.Range(0, _flora.PlantCount).First(i =>
            {
                _flora.TryGetPlant(i, out _, out FloraKind kind);
                return kind == FloraKind.BerryBush;
            });

            StandBeside(index);
            yield return new WaitForSeconds(0.5f);

            _player.ServerNourish(-40f, 0f);
            float before = _player.Satiety;
            var combat = _player.GetComponent<CreatureCombatant>();

            Assert.That(_flora.IsRipe(index), Is.True);
            Assert.That(combat.ServerUseAbility(CreatureAbility.Forage), Is.True, "A plant in reach with fruit on it is eaten.");
            Assert.That(_player.Satiety, Is.GreaterThan(before + 10f), "It fed the creature.");
            Assert.That(_flora.IsRipe(index), Is.False, "The plant is bare.");

            // Eaten out: there is nothing more to take from this one until it has grown back.
            yield return new WaitUntil(() => combat.CooldownRemaining <= 0f && combat.ForageCooldown <= 0f);
            Assert.That(combat.ServerUseAbility(CreatureAbility.Forage), Is.False);

            // Time passes: the server marks it ripe again.
            _flora.ServerRipen(index);
            yield return new WaitForSeconds(0.5f);
            Assert.That(_flora.IsRipe(index), Is.True, "It bears fruit again.");
        }

        [UnityTest]
        public IEnumerator PoisonHurtsAndHungerEmptiesTheStomach()
        {
            _player.ServerNourish(-1000f, 0f);
            Assert.That(_player.IsStarving, Is.True);

            float hp = _player.Hp;
            yield return new WaitForSeconds(2.4f);
            Assert.That(_player.Hp, Is.LessThan(hp), "An empty stomach costs hit points.");
        }

        private static IEnumerator Until(System.Func<bool> predicate, string step)
        {
            float deadline = Time.realtimeSinceStartup + 40f;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail(step + " timed out");
                yield return null;
            }
        }
    }
}
