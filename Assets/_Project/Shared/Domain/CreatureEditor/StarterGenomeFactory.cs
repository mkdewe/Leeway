using System.Collections.Generic;
using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Builds the player's starting kit: three vertebrae plus one random locomotion, mouth and
    /// sense part each.
    /// </summary>
    /// <remarks>
    /// Fully deterministic given the seed — the server derives the starter genome from the
    /// <c>ClientId</c> and the world seed, so the same player in the same session always gets the
    /// same creature, and a test can pin that down.
    /// </remarks>
    public static class StarterGenomeFactory
    {
        public const int StarterVertebraCount = 3;

        // Attachment directions for the starter parts. The length does not matter — only the
        // direction does, because PartPlacement pulls the part onto the body surface anyway.
        private static readonly Vector3 Down = new Vector3(0.35f, -1f, 0f);
        private static readonly Vector3 Front = new Vector3(0f, -0.15f, 1f);
        private static readonly Vector3 UpFrontSide = new Vector3(0.5f, 0.55f, 0.5f);

        private static readonly Color32[] Palette =
        {
            new Color32(120, 180, 110, 255),
            new Color32(190, 140, 90, 255),
            new Color32(110, 150, 200, 255),
            new Color32(200, 120, 140, 255),
            new Color32(170, 170, 100, 255),
        };

        public static CreatureGenome Create(int seed, PartRuleSet rules)
        {
            if (rules == null) rules = PartRuleSet.Empty;

            var rng = new DeterministicRng(seed);
            var genome = new CreatureGenome();

            BuildSpine(genome, ref rng);
            AttachStarterParts(genome, rules, ref rng);
            AssignColors(genome, ref rng);

            return genome;
        }

        private static void BuildSpine(CreatureGenome genome, ref DeterministicRng rng)
        {
            // Vertebra 0 is the head at the armature root; the rest run backwards along -Z.
            float headRadius = Mathf.Clamp(
                GenomeLimits.DefaultHeadRadius * rng.NextRange(0.9f, 1.1f),
                GenomeLimits.MinRadius, GenomeLimits.MaxRadius);

            float torsoRadius = Mathf.Clamp(headRadius * rng.NextRange(1.05f, 1.25f), GenomeLimits.MinRadius, GenomeLimits.MaxRadius);
            float tailRadius = Mathf.Clamp(headRadius * rng.NextRange(0.7f, 0.9f), GenomeLimits.MinRadius, GenomeLimits.MaxRadius);

            genome.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, headRadius));
            genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, torsoRadius));
            genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, tailRadius));
        }

        private static void AttachStarterParts(CreatureGenome genome, PartRuleSet rules, ref DeterministicRng rng)
        {
            // The order matters for determinism — every draw consumes the same stretch of the RNG
            // stream regardless of what the catalog contains.
            //
            // The directions are what places a part on the body: PartPlacement projects them onto the
            // skin and derives the rotation from them. A zero point would mean "on the spine axis" and
            // would fall through to the fallback direction — legs would then stick straight up.
            TryAttachRandom(genome, rules, ref rng, PartCategory.Locomotion, boneIndex: 1, Down);
            TryAttachRandom(genome, rules, ref rng, PartCategory.Mouth, boneIndex: 0, Front);
            TryAttachRandom(genome, rules, ref rng, PartCategory.Sense, boneIndex: 0, UpFrontSide);
        }

        /// <summary>
        /// Draws a part of the given category that fits the site and attaches it. Finding no matching
        /// part is not an exception — the genome simply comes back without it, since no category is
        /// required any more.
        /// </summary>
        private static void TryAttachRandom(CreatureGenome genome, PartRuleSet rules, ref DeterministicRng rng,
            PartCategory category, int boneIndex, Vector3 direction)
        {
            AttachmentSite site = GenomeValidator.SiteForBone(boneIndex, genome.VertebraCount);
            List<PartRule> eligible = rules.GetEligible(category, site);

            // We always consume the RNG, even for an empty list — otherwise a missing category would
            // shift the draws for the following ones and break the seed's determinism.
            int pick = rng.NextInt(Mathf.Max(1, eligible.Count));
            if (eligible.Count == 0) return;

            PartRule rule = eligible[pick];

            // A limb comes with the foot the catalog fits by default. The starter creature has to be
            // able to stand up; choosing something else is what the editor is for.
            int fitting = rule.AcceptsFitting && rules.TryGetRule(rule.DefaultFittingId, out _) ? rule.DefaultFittingId : 0;

            var gene = new PartGene(rule.PartId, (byte)boneIndex, direction, Quaternion.identity, 1f,
                rule.MirrorCapable, false, default, default, 0, fitting);

            GenomeEditOperations.TryAttachPart(genome, gene, rules, out _);
        }

        private static void AssignColors(CreatureGenome genome, ref DeterministicRng rng)
        {
            Color32 primary = Palette[rng.NextInt(Palette.Length)];
            genome.PrimaryColor = primary;
            genome.SecondaryColor = new Color32(
                (byte)(primary.r * 0.55f),
                (byte)(primary.g * 0.55f),
                (byte)(primary.b * 0.55f),
                255);
        }
    }
}
