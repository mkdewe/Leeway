using System;
using System.Collections;
using FishNet.Managing;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>
    /// The Phase 6 gate: being knocked down, lying there, and getting up where the server says.
    /// </summary>
    /// <remarks>
    /// We do not check how the ragdoll looks — the bone poses are deliberately local and differ between
    /// clients. We check what has to be identical everywhere: the knockdown flag, prediction coming to
    /// a halt, and the position after getting up.
    /// </remarks>
    public class CreatureKnockdownTests
    {
        private const float Timeout = 15f;

        private NetworkManager _networkManager;
        private CreatureBody _localBody;
        private CreatureRagdoll _ragdoll;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Application.runInBackground = true;
            yield return CreatureEditorTestScene.EnsureLoaded();

            _networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            Assert.IsNotNull(_networkManager, "The scene has no NetworkManager.");

            yield return WaitUntil(() => !_networkManager.IsServerStarted && !_networkManager.IsClientStarted,
                "The previous host did not release the socket.");

            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();

            yield return WaitUntil(() => _networkManager.IsServerStarted && _networkManager.IsClientStarted, "The host did not come up.");
            yield return WaitUntil(TryBindLocalBody, "The player's creature was not spawned.");

            _ragdoll = _localBody.GetComponent<CreatureRagdoll>();
            Assert.IsNotNull(_ragdoll, "The creature prefab has no CreatureRagdoll component.");

            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NetworkManager manager = _networkManager != null
                ? _networkManager
                : UnityEngine.Object.FindFirstObjectByType<NetworkManager>();

            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                manager.ServerManager.StopConnection(true);
                yield return WaitUntil(() => !manager.IsServerStarted && !manager.IsClientStarted, "The host did not stop after the test.");
            }

            _networkManager = null;
            _localBody = null;
            _ragdoll = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ragdoll_IsPrebuiltAndIdleUntilKnockdown()
        {
            Assert.Greater(_ragdoll.BoneCount, 0,
                "The ragdoll was not created during the body build — building it on the fall costs a frame hitch.");
            Assert.IsFalse(_ragdoll.IsActive, "The ragdoll is active even though the creature is standing.");
            Assert.IsFalse(_localBody.IsKnockedDown);
            yield break;
        }

        [UnityTest]
        public IEnumerator Ragdoll_BoneCountFollowsGenome()
        {
            Assert.AreEqual(_localBody.Genome.VertebraCount, _ragdoll.BoneCount,
                "The ragdoll has a different number of bodies than the genome has vertebrae.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Knockdown_WeakImpulse_DoesNotTipCreature()
        {
            Assert.IsFalse(_localBody.Knockdown(Vector3.forward * 0.5f, 0),
                "A nudge knocked the creature over — the impulse threshold is not working.");
            Assert.IsFalse(_localBody.IsKnockedDown);
            yield break;
        }

        [UnityTest]
        public IEnumerator Knockdown_ActivatesRagdollAndStopsPrediction()
        {
            Assert.IsTrue(_localBody.Knockdown(new Vector3(0f, 3f, 12f), 1), "A strong impulse did not knock the creature over.");

            Assert.IsTrue(_localBody.IsKnockedDown, "The knockdown flag was not set.");
            Assert.IsTrue(_ragdoll.IsActive, "The ragdoll did not start after the knockdown.");

            var capsule = _localBody.GetComponent<CapsuleCollider>();
            Assert.IsFalse(capsule.enabled, "The locomotion capsule is still active — it will fight the ragdoll bodies.");

            var rb = _localBody.GetComponent<Rigidbody>();
            Assert.IsTrue(rb.isKinematic, "The locomotion body is not kinematic — prediction is still moving it.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Knockdown_WhileDown_CannotBeKnockedAgain()
        {
            Assert.IsTrue(_localBody.Knockdown(new Vector3(0f, 3f, 12f), 1));
            Assert.IsFalse(_localBody.Knockdown(new Vector3(0f, 3f, 12f), 1),
                "A creature already down could be knocked over again — StartTick would be overwritten and it would never get up.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Recover_RestoresLocomotionAndClearsFlag()
        {
            _localBody.Knockdown(new Vector3(0f, 3f, 12f), 1);
            yield return WaitUntil(() => _ragdoll.IsActive, "The ragdoll did not start.");

            // A moment of real simulation, so the creature actually falls over.
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            _localBody.Recover();

            Assert.IsFalse(_localBody.IsKnockedDown, "The knockdown flag did not clear.");
            Assert.IsFalse(_ragdoll.IsActive, "The ragdoll is still active after getting up.");
            Assert.IsTrue(_localBody.GetComponent<CapsuleCollider>().enabled, "The locomotion capsule did not come back.");
            Assert.IsFalse(_localBody.GetComponent<Rigidbody>().isKinematic, "The locomotion body was left kinematic — the creature will not move at all.");
        }

        [UnityTest]
        public IEnumerator Recover_StandsCreatureUpright()
        {
            _localBody.Knockdown(new Vector3(6f, 4f, 6f), 2);
            yield return WaitUntil(() => _ragdoll.IsActive, "The ragdoll did not start.");
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();

            _localBody.Recover();

            Vector3 euler = _localBody.transform.eulerAngles;
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, euler.x), 0.01f, "The creature got up tilted about X.");
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, euler.z), 0.01f, "The creature got up tilted about Z.");
            Assert.Greater(_localBody.transform.position.y, -1f, "The creature ended up under the floor.");
        }

        /// <summary>
        /// The creature has to get up at exactly the height its suspension holds it at.
        /// </summary>
        /// <remarks>
        /// This is the gate against the costliest mistake in this mechanic: the standing height used to
        /// be computed as half the locomotion capsule's height, and that capsule <b>lies</b> along Z, so
        /// it was half the body's length. The creature surfaced a metre above the ground and fell
        /// instead of standing. The only correct height is the one the leg spring maintains —
        /// <c>GenomeStatRules.StandHeight</c>.
        /// </remarks>
        [UnityTest]
        public IEnumerator Recover_PlantsCreatureAtItsSuspensionRideHeight()
        {
            float standHeight = GenomeStatRules.StandHeight(_localBody.Genome, _localBody.Rules);
            Assert.Greater(standHeight, 0.01f, "The test creature has no legs — this test has nothing to measure.");

            _localBody.Knockdown(new Vector3(7f, 4f, 7f), 2);
            yield return WaitUntil(() => _ragdoll.IsActive, "The ragdoll did not start.");
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();

            _localBody.Recover();

            const float lift = 0.05f;
            Vector3 origin = _localBody.transform.position + Vector3.up * lift;
            int groundMask = ~((1 << _localBody.gameObject.layer) | (1 << LayerMask.NameToLayer("CreatureRagdoll")));

            Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, groundMask, QueryTriggerInteraction.Ignore),
                "There is no ground under the standing creature — it ended up off the map.");

            Assert.AreEqual(standHeight, hit.distance - lift, 0.05f,
                "The creature got up at a different height than the one its suspension holds it at.");
        }

        /// <summary>
        /// The spine has to return to exactly the rest pose — including the bone <b>positions</b>, not
        /// just the rotations.
        /// </summary>
        /// <remarks>
        /// In ragdoll mode physics moves every bone and writes its <c>localPosition</c>. Restoring only
        /// the rotations left a permanent offset of the whole chain against the locomotion capsule after
        /// every fall: the mesh, the parts and the hips travelled alongside the body the player actually
        /// steers, and the error grew with each further knockdown.
        /// </remarks>
        [UnityTest]
        public IEnumerator Recover_RestoresSpineRestPose()
        {
            Transform[] bones = _localBody.Built.Bones;
            var rest = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localPosition;

            _localBody.Knockdown(new Vector3(9f, 5f, 9f), 2);
            yield return WaitUntil(() => _ragdoll.IsActive, "The ragdoll did not start.");
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();

            yield return WaitUntil(() => !_localBody.IsKnockedDown, "The creature did not get up on its own.");

            // The pose blends in through LitMotion on Update, while the rising phase ends on a network
            // tick — those two clocks can miss each other by a frame.
            yield return new WaitForSeconds(0.25f);

            for (int i = 0; i < bones.Length; i++)
            {
                Assert.Less(Vector3.Distance(rest[i], bones[i].localPosition), 0.001f,
                    $"Vertebra {i} was moved by the ragdoll and did not come back — " +
                    "the whole spine travels alongside the locomotion capsule.");
            }
        }

        /// <summary>
        /// There has to be a visible gap between the ragdoll going off and control being handed back —
        /// that gap is where getting up happens.
        /// </summary>
        /// <remarks>
        /// Previously both happened in the same frame: the creature jumped straight from a sprawled pose
        /// into a run. The rising phase holds the knockdown flag up for exactly as long as the bone pose
        /// takes to blend in.
        /// </remarks>
        [UnityTest]
        public IEnumerator Knockdown_RisesBeforeGivingBackControl()
        {
            _localBody.Knockdown(new Vector3(0f, 3f, 12f), 1);
            yield return WaitUntil(() => _ragdoll.IsActive, "The ragdoll did not start.");

            yield return WaitUntil(() => !_ragdoll.IsActive, "The server did not end the lie-down.");

            Assert.IsTrue(_localBody.IsKnockedDown,
                "Control came back in the same frame the ragdoll went off — there is no time to show the rise.");
            Assert.IsTrue(_localBody.CurrentKnockdown.IsRising, "The state did not enter the rising phase.");

            yield return WaitUntil(() => !_localBody.IsKnockedDown, "The creature did not finish getting up.");
            Assert.IsFalse(_localBody.CurrentKnockdown.IsRising, "The rising phase did not clear along with the knockdown.");
        }

        /// <summary>
        /// The server stands the creature up by itself after <c>_knockdownSeconds</c>. Without that a
        /// knockdown would never end if nobody called <c>Recover</c> by hand.
        /// </summary>
        [UnityTest]
        public IEnumerator Knockdown_ExpiresOnItsOwn()
        {
            _localBody.Knockdown(new Vector3(0f, 3f, 12f), 1);
            Assert.IsTrue(_localBody.IsKnockedDown);

            yield return WaitUntil(() => !_localBody.IsKnockedDown, "The creature did not get up on its own once the knockdown time elapsed.");
            Assert.IsFalse(_ragdoll.IsActive, "The ragdoll survived the automatic rise.");
        }

        /// <summary>
        /// The constraint the whole of Phase 6 rests on — under <c>PhysicsMode.TimeManager</c> the
        /// reconcile replay loop would resimulate every rigidbody in the scene and the ragdoll would
        /// explode. The test guards the setting, because it is a hidden dependency.
        /// </summary>
        [UnityTest]
        public IEnumerator PhysicsMode_StaysUnity()
        {
            Assert.AreEqual(FishNet.Managing.Timing.PhysicsMode.Unity, _networkManager.TimeManager.PhysicsMode,
                "PhysicsMode was switched to TimeManager — ragdolls will be resimulated in the reconcile loop and explode.");
            yield break;
        }

        // ---------- helpers ----------

        private bool TryBindLocalBody()
        {
            foreach (CreatureBody candidate in UnityEngine.Object.FindObjectsByType<CreatureBody>(FindObjectsSortMode.None))
            {
                if (!candidate.IsOwner || candidate.Genome == null || candidate.Built == null) continue;

                _localBody = candidate;
                return true;
            }

            return false;
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
    }
}
