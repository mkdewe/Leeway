using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Ready-made enemies, built out of the same catalog parts the player builds with.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a genome and not a modelled monster.</b> An enemy assembled from catalog parts
    /// obeys every rule the player's creature obeys: its stats come out of
    /// <see cref="GenomeStatRules"/>, its blow out of <see cref="CombatRules"/>, its legs out of the
    /// same leg factory. There is no second set of numbers to keep in step, and a balance change
    /// reaches the enemies on its own. It also costs nothing to ship — an enemy is a few dozen bytes
    /// of genome, not an asset.</para>
    ///
    /// <para><b>Missing parts are survivable.</b> A catalog may be trimmed, and a part may be renamed.
    /// Every attachment here is attempted and dropped if it does not take, so the worst case is a
    /// plainer animal rather than no enemy at all.</para>
    /// </remarks>
    public static class EnemyGenomeLibrary
    {
        /// <summary>Directions parts are hung in. Only the direction matters — the placement pulls the part onto the skin.</summary>
        private static readonly Vector3 Front = new Vector3(0f, -0.1f, 1f);
        private static readonly Vector3 EyeSide = new Vector3(0.55f, 0.5f, 0.6f);
        private static readonly Vector3 Down = new Vector3(0.35f, -1f, 0f);
        private static readonly Vector3 Up = new Vector3(0f, 1f, 0f);

        /// <summary>
        /// The Snapper: a four-vertebra predator on running legs, with a maw, a crest and good eyes.
        /// </summary>
        /// <remarks>
        /// <para>Built to be legible in a fight rather than to be optimal. The maw is what it hurts
        /// with and it is at the front, so a player who keeps their creature's nose away from it takes
        /// no damage; the legs are the running kind, so it closes distance but does not outrun a
        /// creature built for speed; four vertebrae give it enough hit points that the fight lasts more
        /// than one exchange.</para>
        ///
        /// <para><b>It is deliberately no better armed than a starter creature.</b> An earlier build
        /// carried a horn and a spiked back as well, which read beautifully and killed a starting player
        /// in four bites while taking nine to go down — a fight nobody could win with the creature the
        /// game hands them. The measured numbers now: 128 hit points against the starter's 112, and 17
        /// damage against the starter's 10 to 17, depending on which mouth the starter drew. Anything
        /// tougher belongs in a second, later enemy, not in the first one a player meets.</para>
        /// </remarks>
        public static CreatureGenome Snapper(PartRuleSet rules)
        {
            var genome = new CreatureGenome
            {
                PrimaryColor = new Color32(150, 70, 60, 255),
                SecondaryColor = new Color32(70, 40, 38, 255),
            };

            genome.AddVertebra(new VertebraGene(Vector3.zero, Quaternion.identity, 0.30f));
            genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, 0.38f));
            genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, 0.34f));
            genome.AddVertebra(new VertebraGene(GenomeLimits.DefaultSegmentOffset, Quaternion.identity, 0.20f));

            TryAttach(genome, rules, "mouth.maw", 0, Front, mirrored: false);
            TryAttach(genome, rules, "sense.eye", 0, EyeSide, mirrored: true);

            // Running legs on clawed paws: the paw is what the animal digs in with when it turns, and
            // choosing the foot is the same decision the player makes in the editor.
            TryAttach(genome, rules, "loco.runner_legs", 1, Down, mirrored: true, fittingKey: "limb.paw");
            TryAttach(genome, rules, "loco.runner_legs", 2, Down, mirrored: true, fittingKey: "limb.paw");

            return genome;
        }

        /// <summary>
        /// Hangs a part on by its catalog key, and shrugs if the catalog has no such part.
        /// </summary>
        /// <param name="fittingKey">
        /// What to put in the part's socket. Left out, a limb gets whatever the catalog fits by
        /// default — so an enemy is never found standing on stumps because someone forgot a line here.
        /// </param>
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
                if (wanted != 0 && rules.TryGetRule(wanted, out _)) fittingId = wanted;
                else fittingId = rules.TryGetRule(rule.DefaultFittingId, out _) ? rule.DefaultFittingId : 0;
            }

            var gene = new PartGene(partId, (byte)boneIndex, direction, Quaternion.identity, 1f,
                mirrored && rule.MirrorCapable, false, default, default, 0, fittingId);

            return GenomeEditOperations.TryAttachPart(genome, gene, rules, out _);
        }
    }
}
