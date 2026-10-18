using System.Collections;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using Leeway.CreatureEditor;
using Leeway.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>A dead creature crumbles away and leaves the skeleton its build gave it.</summary>
    public class CreatureRemainsPlayTests
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
            foreach (var bone in Object.FindObjectsByType<RemainsLifetime>(FindObjectsSortMode.None)) Object.Destroy(bone.gameObject);

            if (_network)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return Until(() => !_network.IsServerStarted && !_network.IsClientStarted, "host stop");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ADeadCreatureDecaysAndLeavesItsSkeleton()
        {
            var remains = _player.GetComponent<CreatureRemains>();
            Assert.That(remains, Is.Not.Null);
            Assert.That(remains.HasSkeleton, Is.False, "A living creature shows no bones.");

            _player.TakeDamage(99999f);
            yield return new WaitForSeconds(0.6f);

            Assert.That(remains.HasSkeleton, Is.True, "The skeleton is drawn from its build when it dies.");
            Assert.That(remains.Decay, Is.GreaterThan(0f));

            // Long after: the skin is gone, the bones are not.
            typeof(CreatureRemains).GetField("_deadFor", Private).SetValue(remains, 100f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_player.Built.Renderer.enabled, Is.False, "The skin has crumbled away.");

            int pieces = _player.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.gameObject.name == "Vertebra" || r.gameObject.name == "LimbBone");
            Assert.That(pieces, Is.GreaterThan(3), "Spine, ribs and limbs are all there.");
        }

        [UnityTest]
        public IEnumerator TheSkeletonStaysWhenTheBodyIsTakenAway()
        {
            _player.TakeDamage(99999f);
            yield return new WaitForSeconds(0.6f);
            Assert.That(_player.GetComponent<CreatureRemains>().HasSkeleton, Is.True);

            _player.NetworkObject.Despawn();
            yield return new WaitForSeconds(0.5f);

            int left = Object.FindObjectsByType<RemainsLifetime>(FindObjectsSortMode.None).Length;
            Assert.That(left, Is.GreaterThan(3), "The bones are left lying where the body was.");
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
