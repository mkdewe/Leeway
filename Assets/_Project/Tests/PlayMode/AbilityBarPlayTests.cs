using System.Collections;
using System.Linq;
using FishNet.Managing;
using Leeway.Combat;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.World;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>The row of abilities: one slot per thing the body can do, and a cooldown that runs after a blow.</summary>
    public class AbilityBarPlayTests
    {
        private NetworkManager _network;
        private PlaygroundCreatureController _player;
        private Transform _row;
        private Vector3 _padStart;

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
            yield return Until(() => Object.FindObjectsByType<PlaygroundCreatureController>(FindObjectsSortMode.None).Any(b => b.IsOwner), "player body");
            _player = Object.FindObjectsByType<PlaygroundCreatureController>(FindObjectsSortMode.None).First(b => b.IsOwner);

            var bar = Object.FindFirstObjectByType<AbilityBarHud>();
            Assert.That(bar, Is.Not.Null, "The scene has the ability bar.");
            _row = bar.transform.Find("AbilityCanvas/Row");
            _padStart = Object.FindFirstObjectByType<EditorPad>().transform.position;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The scene is shared with other tests: the pad goes back where it was.
            var pad = Object.FindFirstObjectByType<EditorPad>();
            if (pad) pad.transform.position = _padStart;

            if (_network)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return Until(() => !_network.IsServerStarted && !_network.IsClientStarted, "host stop");
            }

            yield return null;
        }

        private string[] Names() => _row.Cast<Transform>().Select(s => s.name).ToArray();

        [UnityTest]
        public IEnumerator TheRowListsTheBodysAbilitiesAndTheirCooldownRuns()
        {
            yield return Until(() => _row.gameObject.activeSelf && _row.childCount > 0, "slots built");
            Debug.Log("Slots: " + string.Join(", ", Names()));
            Assert.That(Names(), Has.Some.Contains("Skok"));
            Assert.That(Names(), Has.None.Contains("Skrzydła"), "No wings, no wing slot.");

            Object.FindFirstObjectByType<EditorPad>().transform.position = new Vector3(200f, 0f, 200f);
            yield return Until(() => !_player.IsInEditorPad, "left the editor pad");

            var combatant = _player.GetComponent<CreatureCombatant>();
            combatant.RequestAttack();
            yield return Until(() => combatant.CooldownRemaining > 0f, "cooldown started");

            Transform first = _row.GetChild(0);
            var time = first.GetComponentsInChildren<TextMeshProUGUI>().First(t => t.name == "Time");
            yield return null;
            Assert.That(time.text, Is.Not.Empty, "The slot counts the cooldown down.");
        }

        [UnityTest]
        public IEnumerator WingsAddTheirSlot()
        {
            yield return Until(() => _row.childCount > 0, "slots built");

            CreatureGenome genome = _player.Genome.Clone();
            int wingId = StableHash.PartId(CreatureFlightRules.WingKey);
            var wing = new PartGene(wingId, 1, new Vector3(1f, 0.75f, 0f), Quaternion.identity, 1f, true, false, default, default, 0, 0);
            Assert.That(GenomeEditOperations.TryAttachPart(genome, wing, _player.Rules, out _), Is.True);
            Assert.That(_player.ServerSetGenome(genome, out _), Is.True);

            yield return Until(() => Names().Any(n => n.Contains("Skrzydła")), "wing slot");
        }

        private static IEnumerator Until(System.Func<bool> condition, string what, float timeout = 20f)
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
