using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Managing;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>
    /// The Phase 3 and 4 gate, run automatically: a FishNet host in a single process, the real scene,
    /// the real prefab.
    /// </summary>
    /// <remarks>
    /// <para>The host is server and client at once, so one process covers the whole path
    /// <c>ServerRpc → validation → SyncVar → rebuild → TargetRpc</c>. What it does <b>not</b> cover:
    /// drift between two machines, and whether the creature looks like a creature. That stays with the
    /// two-instance test run once per phase.</para>
    ///
    /// <para>Every test gets a fresh scene, so the commit cooldown counters and the pad state start
    /// from zero and the tests do not depend on ordering.</para>
    /// </remarks>
    public class CreatureEditorNetworkTests
    {
        private const string ScenePath = "Assets/Scenes/CreatureEditor.unity";
        private const float Timeout = 10f;

        private NetworkManager _networkManager;
        private CreatureBody _localBody;
        private readonly List<GenomeCommitResultMessage> _results = new();
        private IDisposable _resultSubscription;

        private EditorPad _pad;
        private Vector3 _padHomePosition;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Without this the game loop stalls when the editor window loses focus — the test
            // coroutines freeze mid-step, the timeouts never tick and the whole editor looks hung. The
            // project sets this permanently (a networked game needs it anyway: without it alt-tab
            // freezes the client and drops it from the server), but we force it explicitly so the test
            // does not depend on somebody else's configuration.
            Application.runInBackground = true;

            yield return CreatureEditorTestScene.EnsureLoaded();

            _networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            Assert.IsNotNull(_networkManager, "The editor scene has no NetworkManager.");

            // The scene survives the whole run, so every change made to it has to be undone.
            _pad = UnityEngine.Object.FindFirstObjectByType<EditorPad>();
            if (_pad != null) _padHomePosition = _pad.transform.position;

            yield return WaitUntil(() => !_networkManager.IsServerStarted && !_networkManager.IsClientStarted,
                "The previous host did not release the socket.");

            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();

            yield return WaitUntil(() => _networkManager.IsServerStarted && _networkManager.IsClientStarted,
                "The host did not come up within the time allowed.");

            yield return WaitUntil(() => TryFindLocalBody(out _localBody), "The server did not spawn a creature for the host.");
            yield return WaitUntil(() => _localBody.Genome != null, "The creature spawned without a genome.");

            // The editor pad's trigger only fires on a physics step.
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();

            _results.Clear();
            if (GlobalMessagePipe.IsInitialized)
                _resultSubscription = GlobalMessagePipe.GetSubscriber<GenomeCommitResultMessage>().Subscribe(_results.Add);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _resultSubscription?.Dispose();
            _resultSubscription = null;

            if (_pad != null) _pad.transform.position = _padHomePosition;
            _pad = null;

            // The socket has to be released even when a test failed halfway — otherwise the next
            // StartConnection hits a busy port and the whole class falls apart.
            NetworkManager manager = _networkManager != null
                ? _networkManager
                : UnityEngine.Object.FindFirstObjectByType<NetworkManager>();

            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                manager.ServerManager.StopConnection(true);

                // The disconnect has to complete before the next test starts a host: despawning bodies
                // goes through OnRemoteConnectionState, so an unfinished shutdown would leave the
                // creature from the previous test behind.
                yield return WaitUntil(() => !manager.IsServerStarted && !manager.IsClientStarted,
                    "The host did not stop after the test.");
            }

            _localBody = null;
            _networkManager = null;
            yield return null;
        }

        // ---------- Phase 3: spawn ----------

        [UnityTest]
        public IEnumerator Host_ReceivesStarterCreature()
        {
            Assert.AreEqual(StarterGenomeFactory.StarterVertebraCount, _localBody.Genome.VertebraCount,
                "The starter genome does not have the agreed vertebra count.");
            Assert.Greater(_localBody.Stats.MaxHp, 0f, "The creature came up with no HP — the stats were not derived from the genome.");
            Assert.IsTrue(_localBody.IsAlive);
            yield break;
        }

        [UnityTest]
        public IEnumerator Host_CreatureBodyIsBuiltFromGenome()
        {
            Assert.IsNotNull(_localBody.Built, "The body was not built.");
            Assert.AreEqual(_localBody.Genome.VertebraCount, _localBody.Built.Bones.Length,
                "The bone count does not match the genome after the network spawn.");
            Assert.Greater(_localBody.Built.GeneratedMesh.vertexCount, 0, "The body mesh is empty.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Spawn_PlacesCreatureInsideEditorPad()
        {
            Assert.IsTrue(_localBody.IsInEditorPad,
                "The creature did not land in the editor pad — the commit would be blocked from the start.");
            yield break;
        }

        // ---------- Phase 4: commit ----------

        [UnityTest]
        public IEnumerator Commit_AddVertebra_GrowsCreatureEverywhere()
        {
            int before = _localBody.Genome.VertebraCount;
            float hpBefore = _localBody.Stats.MaxHp;

            yield return Commit(AddVertebra(_localBody));

            Assert.IsTrue(LastResult().Accepted, $"The server rejected a valid genome: {LastResult().Error}.");
            Assert.AreEqual(before + 1, _localBody.Genome.VertebraCount, "The genome did not grow after the commit.");
            Assert.AreEqual(before + 1, _localBody.Built.Bones.Length, "The body was not rebuilt after the commit.");
            Assert.Greater(_localBody.Stats.MaxHp, hpBefore, "The extra vertebra did not translate into HP.");
        }

        /// <summary>
        /// Spamming full genomes is a trivial DoS — validation and a mesh rebuild for every packet. A
        /// second commit in the same tick has to be refused.
        /// </summary>
        [UnityTest]
        public IEnumerator Commit_TwiceInSameTick_SecondIsRateLimited()
        {
            _localBody.RequestCommit(AddVertebra(_localBody), out _);
            _localBody.RequestCommit(AddVertebra(_localBody), out _);

            yield return WaitUntil(() => _results.Count >= 2, "Two commit responses did not arrive.");

            Assert.IsTrue(_results[0].Accepted, $"The first commit was rejected: {_results[0].Error}.");
            Assert.IsFalse(_results[1].Accepted, "A second commit in the same tick was accepted — there is no spam protection.");
            Assert.AreEqual(GenomeError.RateLimited, _results[1].Error);
        }

        /// <summary>
        /// The gate exists because of physics: swapping the collider under a moving, predicted
        /// Rigidbody can drop the creature through the ground for a frame.
        /// </summary>
        [UnityTest]
        public IEnumerator Commit_OutsideEditorPad_IsRejected()
        {
            // We move the pad, not the creature: the body is a predicted Rigidbody, so teleporting it
            // through its transform is undone by the next reconcile. All that matters here is
            // separating the trigger-collider pair, and the pad belongs to nobody.
            var pad = UnityEngine.Object.FindFirstObjectByType<EditorPad>();
            Assert.IsNotNull(pad, "The editor scene has no EditorPad.");
            pad.transform.position = new Vector3(200f, 0f, 200f);

            yield return WaitUntil(() => !_localBody.IsInEditorPad, "The creature did not leave the editor pad.");

            int before = _localBody.Genome.VertebraCount;
            yield return Commit(AddVertebra(_localBody));

            Assert.IsFalse(LastResult().Accepted, "A commit outside the editor pad was accepted.");
            Assert.AreEqual(GenomeError.NotInEditorPad, LastResult().Error);
            Assert.AreEqual(before, _localBody.Genome.VertebraCount, "A rejected commit changed the genome anyway.");
        }

        /// <summary>
        /// A structurally invalid genome has to be refused on the client — without sending a packet.
        /// </summary>
        [UnityTest]
        public IEnumerator Commit_StructurallyInvalidGenome_FailsBeforeLeavingClient()
        {
            CreatureGenome broken = _localBody.Genome.Clone();
            Assert.Greater(broken.PartCount, 0, "The starter creature has no part to corrupt.");

            broken.SetPart(0, broken.GetPart(0).WithScale(GenomeLimits.MaxPartScale + 1f));

            LogAssert.ignoreFailingMessages = true;
            bool sent = _localBody.RequestCommit(broken, out GenomeError error);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(sent, "An invalid genome went to the server instead of being refused locally.");
            Assert.AreEqual(GenomeError.ScaleOutOfRange, error);
            yield break;
        }

        /// <summary>Part categories no longer have limits — a legless creature is legal and passes the commit.</summary>
        [UnityTest]
        public IEnumerator Commit_GenomeWithoutLocomotion_IsAccepted()
        {
            CreatureGenome legless = _localBody.Genome.Clone();
            PartRuleSet rules = _localBody.Rules;

            for (int i = legless.PartCount - 1; i >= 0; i--)
            {
                if (rules.TryGetRule(legless.GetPart(i).PartId, out PartRule rule) && rule.Category == PartCategory.Locomotion)
                    Assert.IsTrue(GenomeEditOperations.TryDetachPart(legless, i, out _));
            }

            yield return Commit(legless);

            Assert.IsTrue(LastResult().Accepted, $"A legless creature was rejected: {LastResult().Error}.");
        }

        [UnityTest]
        public IEnumerator Commit_RebuildIsIdempotent_SameGenomeDoesNotRebuild()
        {
            yield return Commit(AddVertebra(_localBody));
            Assert.IsTrue(LastResult().Accepted);

            BuiltCreatureBody built = _localBody.Built;
            CreatureGenome unchanged = _localBody.Genome.Clone();

            yield return WaitTicks(20); // wait out the cooldown
            yield return Commit(unchanged);

            Assert.AreSame(built, _localBody.Built,
                "Committing an identical genome rebuilt the body — every commit would throw the mesh away for nothing.");
        }

        // ---------- helpers ----------

        private static CreatureGenome AddVertebra(CreatureBody body)
        {
            CreatureGenome working = body.Genome.Clone();
            Assert.IsTrue(GenomeEditOperations.TryAddVertebra(working, working.VertebraCount - 1, body.Rules, out GenomeError error),
                $"Failed to add a vertebra: {error}.");
            return working;
        }

        private IEnumerator Commit(CreatureGenome genome)
        {
            int before = _results.Count;
            _localBody.RequestCommit(genome, out _);
            yield return WaitUntil(() => _results.Count > before, "The server's response to the commit did not arrive.");
        }

        private GenomeCommitResultMessage LastResult()
        {
            Assert.Greater(_results.Count, 0, "There is no commit response at all.");
            return _results[_results.Count - 1];
        }

        private IEnumerator WaitTicks(uint ticks)
        {
            uint target = _networkManager.TimeManager.Tick + ticks;
            yield return WaitUntil(() => _networkManager.TimeManager.Tick >= target, "The network clock stopped.");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string failureMessage)
        {
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"{failureMessage} (limit {Timeout} s)");
                yield return null;
            }
        }

        private static bool TryFindLocalBody(out CreatureBody body)
        {
            foreach (CreatureBody candidate in UnityEngine.Object.FindObjectsByType<CreatureBody>(FindObjectsSortMode.None))
            {
                if (!candidate.IsOwner) continue;

                body = candidate;
                return true;
            }

            body = null;
            return false;
        }
    }
}
