using System.Collections;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>
    /// Wings in the running game: a press of the jump key beats them and spends stamina, and holding it
    /// slows the fall.
    /// </summary>
    public class CreatureFlightPlayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private NetworkManager _network;
        private PlaygroundCreatureController _player;

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

        private void GrowWings()
        {
            CreatureGenome genome = _player.Genome.Clone();
            PartRuleSet rules = _player.Rules;
            int wingId = StableHash.PartId(CreatureFlightRules.WingKey);
            Assert.That(rules.TryGetRule(wingId, out _), Is.True, "The catalog has the wing part.");

            var wing = new PartGene(wingId, 1, new Vector3(1f, 0.75f, 0f), Quaternion.identity, 1f, true, false, default, default, 0, 0);
            Assert.That(GenomeEditOperations.TryAttachPart(genome, wing, rules, out _), Is.True);
            Assert.That(_player.ServerSetGenome(genome, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator WingsLiftTheCreatureAndCostStamina()
        {
            GrowWings();
            yield return Until(() => _player.CanFly && !_player.IsKnockedDown, "wings grown");
            yield return new WaitForSeconds(2f);

            var jump = typeof(PlaygroundCreatureController).GetField("_jumpQueued", Private);
            var flap = typeof(PlaygroundCreatureController).GetField("_flapQueued", Private);
            var rb = _player.GetComponent<Rigidbody>();

            float start = _player.transform.position.y;
            float highest = start;
            float stamina = _player.Stamina;

            // A player taps the key: the first press jumps off the ground, the rest beat the wings.
            for (int i = 0; i < 14; i++)
            {
                jump.SetValue(_player, true);
                flap.SetValue(_player, true);
                yield return new WaitForSeconds(0.3f);
                highest = Mathf.Max(highest, rb.position.y);
            }

            Assert.That(highest - start, Is.GreaterThan(1.6f), "Beating the wings climbs well past a leg jump.");
            Assert.That(_player.Stamina, Is.LessThan(stamina - 20f), "Every beat and the jump spent stamina.");
        }

        [UnityTest]
        public IEnumerator HoldingTheKeyGlidesAndSinksSlowly()
        {
            GrowWings();
            yield return Until(() => _player.CanFly && !_player.IsKnockedDown, "wings grown");
            yield return new WaitForSeconds(2f);

            var jump = typeof(PlaygroundCreatureController).GetField("_jumpQueued", Private);
            var flap = typeof(PlaygroundCreatureController).GetField("_flapQueued", Private);
            var glide = typeof(PlaygroundCreatureController).GetField("_glideHeld", Private);
            var rb = _player.GetComponent<Rigidbody>();

            float start = rb.position.y;

            // Climb until well off the ground, then let go and hold the key as it starts to fall.
            for (int i = 0; i < 30 && rb.position.y < start + 2.2f; i++)
            {
                jump.SetValue(_player, true);
                flap.SetValue(_player, true);
                yield return new WaitForSeconds(0.3f);
            }

            Assert.That(rb.position.y, Is.GreaterThan(start + 1.2f), "The climb got off the ground.");
            yield return new WaitUntil(() => rb.linearVelocity.y < -0.5f);

            float sink = float.MaxValue;
            for (int i = 0; i < 25; i++)
            {
                glide.SetValue(_player, true);
                yield return null;
                if (rb.position.y > start + 0.9f) sink = Mathf.Min(sink, -rb.linearVelocity.y);
            }

            glide.SetValue(_player, false);
            Assert.That(sink, Is.LessThan(CreatureFlightRules.GlideSinkSpeed(CreatureFlightRules.WingPower(_player.Genome, _player.Rules), _player.Stats.Mass) + 0.8f),
                "Gliding keeps the fall at the wings' sink speed.");
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
