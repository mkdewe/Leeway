using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// A mirrored pair has to <b>be</b> a mirrored pair — on the real catalog, with a real build.
    /// </summary>
    /// <remarks>
    /// Both failures these tests pin were invisible to every other check in the project: the creature
    /// built, nothing threw, the stats were right, and it simply looked wrong. One had a limb's foot
    /// sitting on the same side of the body on both legs; the other turned both legs of a pair the same
    /// way round the world, so splaying one leg outwards swung the other one inwards.
    /// </remarks>
    public class MirroredPartTests
    {
        private const string CatalogPath = "Assets/_Project/Features/CreatureEditor/Data/PartCatalog.asset";
        private const string SettingsPath = "Assets/_Project/Features/CreatureEditor/Data/BodyBuildSettings.asset";

        private const string LegKey = "loco.hoof_legs";

        private CreaturePartCatalog _catalog;
        private CreatureBodyBuildSettings _settings;
        private PartRuleSet _rules;
        private GameObject _root;
        private BuiltCreatureBody _built;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<CreaturePartCatalog>(CatalogPath);
            _settings = AssetDatabase.LoadAssetAtPath<CreatureBodyBuildSettings>(SettingsPath);

            Assert.IsNotNull(_catalog, $"No part catalog at {CatalogPath}.");
            Assert.IsNotNull(_settings, $"No build settings at {SettingsPath}.");

            _rules = _catalog.BuildRuleSet();
            _root = new GameObject("MirrorTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            _built?.Dispose();
            _built = null;

            if (_root != null) Object.DestroyImmediate(_root);
        }

        /// <summary>A four-vertebra body with one mirrored pair of legs, turned by the given correction.</summary>
        private void BuildPair(Quaternion correction, out Transform right, out Transform left)
        {
            var genome = new CreatureGenome();
            for (int i = 0; i < 4; i++)
                genome.AddVertebra(new VertebraGene(i == 0 ? Vector3.zero : GenomeLimits.DefaultSegmentOffset,
                    Quaternion.identity, 0.3f));

            int legId = StableHash.PartId(LegKey);
            Assert.IsTrue(_rules.TryGetRule(legId, out PartRule rule), $"The catalog has no {LegKey}.");

            var gene = new PartGene(legId, 1, new Vector3(0.3f, -0.95f, 0f), correction, 1f, true,
                false, default, default, 0, rule.DefaultFittingId);

            Assert.IsTrue(GenomeEditOperations.TryAttachPart(genome, gene, _rules, out GenomeError error), $"{error}");

            _built = CreatureBodyBuilder.Build(genome, _catalog, _settings, _root.transform, CreatureStatTuning.Default);
            Assert.IsNotNull(_built);

            right = null;
            left = null;

            foreach (CreaturePartInstance instance in _built.PartInstances)
            {
                if (instance.Object == null) continue;

                if (instance.Mirrored) left = instance.Object.transform;
                else right = instance.Object.transform;
            }

            Assert.IsNotNull(right, "The right-hand leg was not built.");
            Assert.IsNotNull(left, "The mirrored leg was not built.");
        }

        /// <summary>Mirror symmetry: the same point with its x reversed.</summary>
        private static void AssertMirrored(Vector3 right, Vector3 left, string what)
        {
            Assert.AreEqual(-right.x, left.x, 1e-3f, $"{what}: the two sides are not opposite across the body.");
            Assert.AreEqual(right.y, left.y, 1e-3f, $"{what}: the mirrored one sits at a different height.");
            Assert.AreEqual(right.z, left.z, 1e-3f, $"{what}: the mirrored one sits further along the body.");
        }

        [Test]
        public void AnUnturnedPair_IsSymmetric()
        {
            BuildPair(Quaternion.identity, out Transform right, out Transform left);

            AssertMirrored(right.position, left.position, "the hips");
            AssertMirrored(right.forward, left.forward, "the limb axes");
        }

        /// <summary>
        /// The report this test exists for: turning a leg turned its twin the same way round the world,
        /// so a pair splayed out to one side instead of out to both.
        /// </summary>
        [Test]
        public void ATurnedPair_TurnsTheOppositeWay()
        {
            BuildPair(Quaternion.AngleAxis(25f, Vector3.up), out Transform right, out Transform left);

            AssertMirrored(right.forward, left.forward, "the turned limb axes");
            Assert.Greater(Mathf.Abs(right.forward.x - left.forward.x), 1e-3f,
                "A turn that leaves both legs pointing the same way is the bug this test is about.");
        }

        [Test]
        public void ATurnedPair_KeepsItsFeetOnItsOwnSide()
        {
            BuildPair(Quaternion.AngleAxis(25f, Vector3.up), out Transform right, out Transform left);

            Transform rightFoot = FindFitting(right);
            Transform leftFoot = FindFitting(left);

            Assert.IsNotNull(rightFoot, "The right leg has no foot in its socket.");
            Assert.IsNotNull(leftFoot, "The mirrored leg has no foot in its socket.");

            AssertMirrored(rightFoot.position - _root.transform.position,
                leftFoot.position - _root.transform.position, "the feet");
        }

        private static Transform FindFitting(Transform limb)
        {
            foreach (Transform node in limb.GetComponentsInChildren<Transform>(true))
                if (node.name.StartsWith("limb.")) return node;

            return null;
        }
    }
}
