using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// A hand-built set of part rules and the typical genomes used by the tests. The whole point of the
    /// domain layer is that it can be run without a ScriptableObject and without the Unity runtime —
    /// these fixtures are the proof.
    /// </summary>
    internal static class GenomeTestFixtures
    {
        public static readonly int LegsId = StableHash.PartId("loco.legs");
        public static readonly int FinId = StableHash.PartId("loco.tail_fin");
        public static readonly int JawId = StableHash.PartId("mouth.jaw");
        public static readonly int EyeId = StableHash.PartId("sense.eye");
        public static readonly int AntennaId = StableHash.PartId("sense.antenna");
        public static readonly int UnknownId = StableHash.PartId("nope.missing");

        public const int VertebraCost = 10;
        public const int PartCost = 25;

        /// <summary>A pool with room to spare — used everywhere the test is not about the budget itself.</summary>
        public const int GenerousBudget = 1000000;

        /// <summary>
        /// The test catalog. <c>loco.tail_fin</c> is deliberately allowed on the tail only — which makes
        /// it possible to check that adding a vertebra (which turns the last vertebra into torso)
        /// invalidates the attachment and the operation rolls back.
        /// </summary>
        public static PartRuleSet Rules(int budget = GenerousBudget) => new PartRuleSet(new[]
        {
            // Locomotion parts get a real leg build — without one the reach is zero and everything that
            // computes stance or gait comes out degenerate.
            new PartRule(LegsId, PartCategory.Locomotion, AttachmentSite.Torso, true,
                new PartStatContribution { SpeedBonus = 1.5f, MassKg = 1.2f }, PartCost, new LegSpec(2, 0.26f)),

            new PartRule(FinId, PartCategory.Locomotion, AttachmentSite.Tail, true,
                new PartStatContribution { SpeedBonus = 2.5f, MassKg = 0.6f }, PartCost, new LegSpec(3, 0.16f)),

            new PartRule(JawId, PartCategory.Mouth, AttachmentSite.Head, false,
                new PartStatContribution { DamageBonus = 7f, HpBonus = 5f, MassKg = 0.8f }, PartCost),

            new PartRule(EyeId, PartCategory.Sense, AttachmentSite.Head, true,
                new PartStatContribution { SenseRadiusBonus = 4f, MassKg = 0.1f }, PartCost),

            new PartRule(AntennaId, PartCategory.Sense, AttachmentSite.Head | AttachmentSite.Torso, false,
                new PartStatContribution { SenseRadiusBonus = 2f, MassKg = 0.05f }, PartCost),
        }, VertebraCost, budget);

        /// <summary>Three vertebrae with no parts — the base for building test cases.</summary>
        public static CreatureGenome Spine(int vertebraCount = 3, float radius = 0.3f)
        {
            var genome = new CreatureGenome();
            genome.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, radius));
            for (int i = 1; i < vertebraCount; i++)
                genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, radius));
            return genome;
        }

        /// <summary>A complete, valid genome: legs on the torso, a jaw and an eye on the head.</summary>
        public static CreatureGenome Valid()
        {
            CreatureGenome genome = Spine();
            genome.AddPart(PartGene.Default(LegsId, 1, mirrored: true));
            genome.AddPart(PartGene.Default(JawId, 0, mirrored: false));
            genome.AddPart(PartGene.Default(EyeId, 0, mirrored: true));
            return genome;
        }
    }
}
