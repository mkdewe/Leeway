using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Creatures built by hand to show what the editor can do — the equivalent of a good screenshot,
    /// kept as data rather than as a picture.
    /// </summary>
    /// <remarks>
    /// <para>Like <see cref="EnemyGenomeLibrary"/>, these are made of catalog parts and nothing else, so
    /// a player can load one, take it apart and see exactly how it was done. Every attachment is
    /// allowed to fail quietly: a trimmed catalog gives a plainer creature, never an exception.</para>
    /// </remarks>
    public static class ShowcaseGenomes
    {
        /// <summary>
        /// An upright, two-legged, two-armed creature.
        /// </summary>
        /// <remarks>
        /// <para><b>The spine stands up.</b> Nothing in the genome says a creature is a quadruped —
        /// vertebra offsets are free vectors, so a spine that steps <b>downwards</b> from the head
        /// gives a torso with a head on top and hips at the bottom, and the parts then land where they
        /// would on a person: face at the front of the head, arms off the chest, legs under the hips.
        /// The body is the same rig a four-legged animal uses.</para>
        ///
        /// <para><b>The proportions are the point.</b> A narrow neck between a wide head and a wider
        /// chest is what makes a silhouette read as a person rather than as a sausage: the eye follows
        /// the waist in and out again. The hips are a touch wider than the waist so the legs have
        /// somewhere to hang from.</para>
        /// </remarks>
        public static CreatureGenome Humanoid(PartRuleSet rules)
        {
            var genome = new CreatureGenome
            {
                // A warm, slightly dusty skin, with the limbs a few shades deeper — the oldest trick
                // for making a figure read at a distance.
                PrimaryColor = new Color32(198, 154, 122, 255),
                SecondaryColor = new Color32(116, 86, 68, 255),

                // The coat is a light speckle: it breaks up the flat shading without turning the
                // creature into an animal print.
                BodyPattern = 5,
            };

            // Head, neck, chest, waist, hips — each step downwards from the one before. The waist is
            // deliberately the narrowest thing on the creature: a silhouette that goes wide, narrow,
            // wide is what the eye reads as a torso rather than as a sack.
            genome.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, 0.24f));
            genome.AddVertebra(new VertebraGene(new Vector3(0f, -0.28f, 0f), Quaternion.identity, 0.11f));
            genome.AddVertebra(new VertebraGene(new Vector3(0f, -0.26f, 0f), Quaternion.identity, 0.30f));
            genome.AddVertebra(new VertebraGene(new Vector3(0f, -0.30f, 0f), Quaternion.identity, 0.18f));
            genome.AddVertebra(new VertebraGene(new Vector3(0f, -0.26f, 0f), Quaternion.identity, 0.26f));

            TryAttach(genome, rules, "mouth.maw", 0, new Vector3(0f, -0.25f, 1f), mirrored: false);
            TryAttach(genome, rules, "sense.eye", 0, new Vector3(0.45f, 0.30f, 0.85f), mirrored: true);

            // Arms off the chest, angled down rather than straight out — a T-pose is a rig, not a pose.
            TryAttach(genome, rules, "grasp.claw_arm", 2, new Vector3(1f, -0.35f, 0.1f), mirrored: true);

            // Legs under the hips: the longest leg in the catalog, on a padded foot. The short ones
            // left the figure standing on stumps, and a hoof under a humanoid reads as a faun — which
            // is a fine creature, but a different one from this.
            // Out to the side rather than straight down: at the end of a standing spine, "down" is
            // along the spine itself, and the placement has nothing perpendicular left to work with —
            // both legs then converge on the same point under the middle of the hips.
            TryAttach(genome, rules, "loco.hoof_legs", 4, new Vector3(1f, -0.35f, 0f), mirrored: true,
                fittingKey: "limb.paw");

            return genome;
        }

        /// <summary>Hangs a part on by its catalog key, with whatever the catalog fits into it by default.</summary>
        private static bool TryAttach(CreatureGenome genome, PartRuleSet rules, string partKey, int boneIndex,
            Vector3 direction, bool mirrored, string fittingKey = null)
        {
            if (rules == null) return false;

            int partId = StableHash.PartId(partKey);
            if (!rules.TryGetRule(partId, out PartRule rule)) return false;

            int fittingId = 0;
            if (rule.AcceptsFitting)
            {
                int wanted = string.IsNullOrEmpty(fittingKey) ? rule.DefaultFittingId : StableHash.PartId(fittingKey);
                fittingId = rules.TryGetRule(wanted, out _) ? wanted : rule.DefaultFittingId;
            }

            var gene = new PartGene(partId, (byte)boneIndex, direction, Quaternion.identity, 1f,
                mirrored && rule.MirrorCapable, false, default, default, 0, fittingId);

            return GenomeEditOperations.TryAttachPart(genome, gene, rules, out _);
        }
    }
}
