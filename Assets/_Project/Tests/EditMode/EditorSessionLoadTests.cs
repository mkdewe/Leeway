using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Opening a session and loading a creature into one are different things, and the difference is
    /// what Apply goes by.
    /// </summary>
    /// <remarks>
    /// The bug this pins: the preset panel opened a session on the creature it had just loaded, which
    /// declares "this is what the game already has". Apply then found nothing to commit, skipped
    /// straight to the playground, and the player watched their <b>previous</b> creature spawn.
    /// </remarks>
    public class EditorSessionLoadTests
    {
        private GameObject _host;
        private CreatureEditorSession _session;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("SessionHost");
            _session = _host.AddComponent<CreatureEditorSession>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        private static CreatureGenome Spine(int vertebrae, float radius)
        {
            var genome = new CreatureGenome();
            for (int i = 0; i < vertebrae; i++)
                genome.AddVertebra(new VertebraGene(i == 0 ? Vector3.zero : GenomeLimits.DefaultSegmentOffset,
                    Quaternion.identity, radius));

            return genome;
        }

        [Test]
        public void Begin_DeclaresNothingToApply()
        {
            _session.Begin(Spine(3, 0.3f));

            Assert.IsFalse(_session.HasUnappliedChanges, "A session opened on the creature in the game has nothing to commit.");
        }

        [Test]
        public void Load_IsAChangeWaitingToBeApplied()
        {
            _session.Begin(Spine(3, 0.3f));
            _session.Load(Spine(5, 0.2f));

            Assert.IsTrue(_session.HasUnappliedChanges, "A loaded preset is not yet the creature in the game.");
            Assert.AreEqual(5, _session.Working.VertebraCount, "The loaded creature is the one on the bench.");
        }

        [Test]
        public void Load_TakesACopy()
        {
            CreatureGenome source = Spine(4, 0.25f);
            _session.Begin(Spine(3, 0.3f));
            _session.Load(source);

            source.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, 0.25f));

            Assert.AreEqual(4, _session.Working.VertebraCount, "Editing the source afterwards must not reach into the session.");
        }

        /// <summary>Revert belongs to the creature in the game, not to the preset that was tried on.</summary>
        [Test]
        public void Revert_AfterLoad_ReturnsToTheCreatureInTheGame()
        {
            _session.Begin(Spine(3, 0.3f));
            _session.Load(Spine(6, 0.2f));
            _session.Revert();

            Assert.AreEqual(3, _session.Working.VertebraCount);
            Assert.IsFalse(_session.HasUnappliedChanges);
        }

        [Test]
        public void MarkApplied_AfterLoad_MakesTheLoadedCreatureTheNewBaseline()
        {
            _session.Begin(Spine(3, 0.3f));
            _session.Load(Spine(5, 0.2f));
            _session.MarkApplied();
            _session.Revert();

            Assert.AreEqual(5, _session.Working.VertebraCount, "Once applied, the loaded creature is what Revert returns to.");
            Assert.IsFalse(_session.HasUnappliedChanges);
        }

        [Test]
        public void LoadingNothing_LeavesTheSessionAlone()
        {
            _session.Begin(Spine(3, 0.3f));
            _session.Load(null);

            Assert.AreEqual(3, _session.Working.VertebraCount);
            Assert.IsFalse(_session.HasUnappliedChanges);
        }
    }
}
