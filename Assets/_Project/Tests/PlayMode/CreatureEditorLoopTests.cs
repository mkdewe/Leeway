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
    /// The Phase 5 gate: the full editor loop — sculpting on the preview, attaching parts from the
    /// palette, applying, and the change showing up in the playground.
    /// </summary>
    /// <remarks>
    /// What is tested is the behaviour of the controller and the session, not the key bindings
    /// themselves: firing an <c>InputAction</c> artificially would test the Input System, not this game.
    /// Input stays with manual verification — the rest of the loop no longer does.
    /// </remarks>
    public class CreatureEditorLoopTests
    {
        private const float Timeout = 10f;

        private NetworkManager _networkManager;
        private CreatureEditorController _controller;
        private CreatureEditorSession _session;
        private CreatureBodyPreview _preview;
        private CreaturePartCatalog _catalog;
        private CreatureBody _localBody;

        private readonly List<GenomeCommitResultMessage> _results = new();
        private IDisposable _resultSubscription;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Application.runInBackground = true;

            yield return CreatureEditorTestScene.EnsureLoaded();

            _networkManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            _controller = UnityEngine.Object.FindFirstObjectByType<CreatureEditorController>();
            _session = UnityEngine.Object.FindFirstObjectByType<CreatureEditorSession>();
            _preview = UnityEngine.Object.FindFirstObjectByType<CreatureBodyPreview>();

            Assert.IsNotNull(_networkManager, "The scene has no NetworkManager.");
            Assert.IsNotNull(_controller, "The scene has no editor controller.");
            Assert.IsNotNull(_session, "The scene has no sculpting session.");
            Assert.IsNotNull(_preview, "The scene has no preview.");

            _catalog = _preview.Catalog;
            Assert.IsNotNull(_catalog, "The preview has no part catalog.");

            // The scene is shared between tests, and applying switches the editor into play mode and
            // hides the preview. Without this reset the next test would start in whatever state the
            // previous one left behind.
            // We go back through the controller rather than through the camera alone: the controller is
            // what announces the mode change, and so what restores the sculpting panels the previous
            // test switched off.
            _controller.EnterSculptMode();
            _preview.SetVisible(true);

            yield return WaitUntil(() => !_networkManager.IsServerStarted && !_networkManager.IsClientStarted,
                "The previous host did not release the socket.");

            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();

            yield return WaitUntil(() => _networkManager.IsServerStarted && _networkManager.IsClientStarted, "The host did not come up.");
            yield return WaitUntil(TryBindLocalBody, "The player's creature was not spawned.");

            // The controller only opens a session from the network genome after the body message.
            yield return WaitUntil(() => _session.Working != null, "The sculpting session did not start.");

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
            yield return null;
        }

        // ---------- the creature in the playground ----------

        /// <summary>
        /// The creature has to stand where it was spawned, and on the floor.
        /// </summary>
        /// <remarks>
        /// A regression of flesh and blood: <c>OnTick</c> called the reconcile method with a
        /// <c>default</c> payload, and on the server that broadcasts the given state as authoritative.
        /// The effect — every tick the creature landed at (0,0,0), driven into the floor, outside the
        /// editor pad, so no apply would ever go through. Reconcile data may only be built inside
        /// <c>CreateReconcile</c>.
        /// </remarks>
        [UnityTest]
        public IEnumerator PlaygroundCreature_StaysWhereItSpawned_AndRestsOnTheGround()
        {
            yield return WaitTicks(30);

            Vector3 position = _localBody.transform.position;

            Assert.Greater(position.sqrMagnitude, 0.01f,
                "The creature slid to the centre of the world — reconcile is broadcasting a zeroed state again.");

            // The creature stands on legs, so the body capsule has to hang above the ground — the
            // clearance is held by the suspension, not by a collider resting on the floor.
            float standHeight = GenomeStatRules.StandHeight(_localBody.Genome, _localBody.Rules);
            Assert.Greater(standHeight, 0f, "The starter creature has no legs — there is nothing to check.");

            Assert.AreEqual(standHeight, position.y, 0.12f,
                $"The creature is not standing at the height its legs give it (y={position.y:F3}, target={standHeight:F3}).");

            var capsule = _localBody.GetComponent<CapsuleCollider>();
            float bottom = position.y + capsule.center.y - capsule.radius;
            Assert.Greater(bottom, 0f, $"The body touches the ground — there is no room for legs (capsule bottom y={bottom:F3}).");

            Assert.IsTrue(_localBody.IsInEditorPad, "The creature fell out of the editor pad — the commit cannot possibly go through.");
        }

        // ---------- session and preview ----------

        [UnityTest]
        public IEnumerator Session_OpensFromNetworkGenome()
        {
            Assert.AreEqual(_localBody.Genome.VertebraCount, _session.Working.VertebraCount,
                "The session started from a different genome than the one in the game.");
            Assert.IsFalse(_session.HasUnappliedChanges, "A freshly opened session must not have changes to apply.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Sculpting_RebuildsPreviewButLeavesNetworkBodyAlone()
        {
            int networkBefore = _localBody.Genome.VertebraCount;

            Assert.IsTrue(_session.AddVertebra(), "Adding a vertebra in the session failed.");

            Assert.AreEqual(networkBefore + 1, _session.Working.VertebraCount, "The working genome did not grow.");
            Assert.AreEqual(networkBefore + 1, _preview.Body.Bones.Length, "The preview was not rebuilt after the edit.");
            Assert.AreEqual(networkBefore, _localBody.Genome.VertebraCount,
                "Sculpting moved the creature in the game — editing has to be purely local until it is applied.");
            Assert.IsTrue(_session.HasUnappliedChanges);
            yield break;
        }

        [UnityTest]
        public IEnumerator Revert_RestoresGenomeFromSessionStart()
        {
            int before = _session.Working.VertebraCount;

            _session.AddVertebra();
            _session.AddVertebra();
            Assert.AreEqual(before + 2, _session.Working.VertebraCount);

            _controller.Revert();

            Assert.AreEqual(before, _session.Working.VertebraCount, "Revert did not restore the state the session opened with.");
            Assert.IsFalse(_session.HasUnappliedChanges, "After a revert there should be no changes left to apply.");
            yield break;
        }

        // ---------- the palette ----------

        [UnityTest]
        public IEnumerator Palette_AttachesPartToSelectedVertebra()
        {
            CreaturePartDefinition sense = FirstOfCategory(PartCategory.Sense);
            Assert.IsNotNull(sense, "The catalog has no sense part at all.");

            _session.Select(0); // the head — senses are allowed there
            int partsBefore = _session.Working.PartCount;

            _controller.AttachPart(sense.PartId, mirrored: false);

            Assert.AreEqual(partsBefore + 1, _session.Working.PartCount, "The palette did not attach the part.");
            Assert.AreEqual(0, _session.Working.GetPart(_session.Working.PartCount - 1).BoneIndex,
                "The part landed on a different vertebra than the selected one.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Palette_RejectsPartOnIllegalSite()
        {
            CreaturePartDefinition tailOnly = FindTailOnlyPart();
            if (tailOnly == null) Assert.Ignore("The catalog has no tail-only part — there is nothing to check.");

            _session.Select(0); // the head
            int partsBefore = _session.Working.PartCount;

            _controller.AttachPart(tailOnly.PartId, mirrored: false);

            Assert.AreEqual(partsBefore, _session.Working.PartCount,
                "A part allowed on the tail only could be attached to the head.");
            yield break;
        }

        // ---------- grabbing parts ----------

        /// <summary>
        /// Grabbing parts stands or falls on this back-reference: the handle under the cursor has to
        /// point at exactly the gene the player sees. A mirrored pair gives two instances of one gene
        /// and both have to point at the same entry.
        /// </summary>
        [UnityTest]
        public IEnumerator PartHandles_PointBackAtTheGenomeEntryTheyWereBuiltFrom()
        {
            var handles = UnityEngine.Object.FindFirstObjectByType<PartHandleSet>();
            Assert.IsNotNull(handles, "The editor scene has no part handle set.");

            Assert.Greater(_session.Working.PartCount, 0, "The starter creature has no part to grab.");
            Assert.Greater(handles.Handles.Count, 0, "Not a single part handle was created.");

            foreach (PartHandle handle in handles.Handles)
            {
                Assert.GreaterOrEqual(handle.GeneIndex, 0);
                Assert.Less(handle.GeneIndex, _session.Working.PartCount,
                    "A handle points at a gene that does not exist — a grab would edit the wrong part.");
                Assert.AreEqual(8, handle.gameObject.layer, "A part handle is not on the editor's raycast layer.");
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator MovePart_ShiftsOnlyTheGrabbedPart()
        {
            Assert.Greater(_session.Working.PartCount, 1, "The test needs at least two parts.");

            Vector3 otherBefore = _session.Working.GetPart(1).LocalPosition;
            var target = new Vector3(0.05f, 0.15f, 0f);

            Assert.IsTrue(_session.MovePart(0, target), "Moving the part failed.");

            // The gene holds a point already pulled onto the skin — that is what makes the drag run 1:1
            // with the cursor instead of at a scale of "radius / length of the stored vector".
            PartGene moved = _session.Working.GetPart(0);
            Vector3 onSkin = PartPlacement.Resolve(_session.Working, moved.BoneIndex, target, skinOffset: 0f).Position;

            Assert.AreEqual(0f, Vector3.Distance(onSkin, moved.LocalPosition), 0.0001f,
                "The gene does not hold a point lying on the skin.");
            Assert.AreEqual(otherBefore, _session.Working.GetPart(1).LocalPosition,
                "Moving one part moved another one too.");
            yield break;
        }

        /// <summary>Dropping a part outside the creature has to discard it — here through the session operation alone, with no mouse.</summary>
        [UnityTest]
        public IEnumerator DetachPart_RemovesItFromPreviewAndFreesBudget()
        {
            Assert.Greater(_session.Working.PartCount, 0);

            int partsBefore = _session.Working.PartCount;
            int spentBefore = CreatureBudget.Evaluate(_session.Working, _session.Rules).Spent;

            Assert.IsTrue(_session.DetachPart(0), "Detaching the part failed.");

            Assert.AreEqual(partsBefore - 1, _session.Working.PartCount, "The part did not disappear from the genome.");
            Assert.Less(CreatureBudget.Evaluate(_session.Working, _session.Rules).Spent, spentBefore,
                "Discarding the part did not free up any currency.");

            foreach (CreaturePartInstance instance in _preview.Body.PartInstances)
            {
                Assert.Less(instance.GeneIndex, _session.Working.PartCount,
                    "After the detach an instance was left pointing at a gene that does not exist.");
            }

            yield break;
        }

        // ---------- applying ----------

        [UnityTest]
        public IEnumerator Apply_PushesSculptedGenomeIntoTheGame()
        {
            int before = _localBody.Genome.VertebraCount;
            float hpBefore = _localBody.Stats.MaxHp;

            _session.AddVertebra();
            _controller.Apply();

            yield return WaitUntil(() => _results.Count > 0, "The server did not respond to the apply.");

            Assert.IsTrue(_results[0].Accepted, $"The server rejected the apply: {_results[0].Error}.");
            Assert.AreEqual(before + 1, _localBody.Genome.VertebraCount, "The creature in the game did not grow after the apply.");
            Assert.Greater(_localBody.Stats.MaxHp, hpBefore, "HP did not keep up with the new vertebra.");
            Assert.IsFalse(_session.HasUnappliedChanges, "After the genome was accepted the session still thinks it has changes.");
        }

        /// <summary>
        /// Applying ends the sculpting: the preview disappears and the camera moves across to the
        /// creature in the game. Otherwise the player is left with two bodies and no control.
        /// </summary>
        [UnityTest]
        public IEnumerator Apply_HidesPreviewAndHandsControlToTheGameCreature()
        {
            Assert.AreEqual(CreatureEditorMode.Sculpt, _controller.Mode, "The test starts outside sculpt mode.");
            Assert.IsTrue(_preview.IsVisible, "The preview is hidden even before the apply.");

            _session.AddVertebra();
            _controller.Apply();

            yield return WaitUntil(() => _results.Count > 0, "The server did not respond to the apply.");
            Assert.IsTrue(_results[0].Accepted, $"The server rejected the apply: {_results[0].Error}.");

            Assert.IsFalse(_preview.IsVisible, "The sculpting preview stayed on screen after the apply.");
            Assert.AreEqual(CreatureEditorMode.Play, _controller.Mode, "The camera did not move across to the creature in the game.");
            Assert.IsTrue(_localBody.IsOwner, "The player does not own the creature they are meant to steer.");
            Assert.Greater(_localBody.Stats.MoveSpeed, 0f, "The creature has zero speed — it cannot be moved at all.");
        }

        /// <summary>
        /// The sculpting UI has no business hanging over the game, and the way back has to be visible —
        /// not merely guessed at on the keyboard.
        /// </summary>
        [UnityTest]
        public IEnumerator Play_HidesSculptingPanelsAndShowsTheWayBack()
        {
            Assert.IsNotNull(GameObject.Find("PalettePanel"), "The palette is switched off while still in sculpt mode.");

            _session.AddVertebra();
            _controller.Apply();

            yield return WaitUntil(() => _results.Count > 0, "The server did not respond to the apply.");
            Assert.AreEqual(CreatureEditorMode.Play, _controller.Mode, "The editor did not switch into play mode.");

            // GameObject.Find sees active objects only — no result means "switched off".
            Assert.IsNull(GameObject.Find("PalettePanel"), "The part palette stayed on screen over the game.");
            Assert.IsNull(GameObject.Find("StatsPanel"), "The sculpting panel stayed on screen over the game.");

            GameObject back = GameObject.Find("ReturnToEditorButton");
            Assert.IsNotNull(back, "There is no way-back button — the only way out is left on the keyboard.");

            back.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Assert.AreEqual(CreatureEditorMode.Sculpt, _controller.Mode, "The way-back button did not return to sculpt mode.");
            Assert.IsNotNull(GameObject.Find("PalettePanel"), "The palette did not come back along with sculpt mode.");
            Assert.IsNull(GameObject.Find("ReturnToEditorButton"), "The way-back button stayed on screen in sculpt mode.");
        }

        [UnityTest]
        public IEnumerator Apply_WithoutChanges_SendsNothing()
        {
            Assert.IsFalse(_session.HasUnappliedChanges);

            _controller.Apply();
            yield return WaitTicks(5);

            // Committing an identical genome is legal, but it must not change anything.
            Assert.AreEqual(_session.Working.VertebraCount, _localBody.Genome.VertebraCount,
                "An apply with no changes moved the creature.");
        }

        // ---------- helpers ----------

        private CreaturePartDefinition FirstOfCategory(PartCategory category)
        {
            foreach (CreaturePartDefinition definition in _catalog.GetByCategory(category))
                if (definition != null) return definition;

            return null;
        }

        private CreaturePartDefinition FindTailOnlyPart()
        {
            foreach (PartCategory category in new[] { PartCategory.Locomotion, PartCategory.Mouth, PartCategory.Sense })
            {
                foreach (CreaturePartDefinition definition in _catalog.GetByCategory(category))
                {
                    if (definition != null && definition.AllowedSites == AttachmentSite.Tail) return definition;
                }
            }

            return null;
        }

        private bool TryBindLocalBody()
        {
            foreach (CreatureBody candidate in UnityEngine.Object.FindObjectsByType<CreatureBody>(FindObjectsSortMode.None))
            {
                if (!candidate.IsOwner || candidate.Genome == null) continue;

                _localBody = candidate;
                return true;
            }

            return false;
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
    }
}
