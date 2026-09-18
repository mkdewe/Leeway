using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Picks the locomotion parts out of a finished body and unfolds them into legs.
    /// </summary>
    /// <remarks>
    /// <para>One place for sculpting and for the playground. Keeping them apart produced exactly the
    /// bug this class was created for: the preview showed the part's <b>authored capsule</b> while the
    /// playground unfolded it into a bending chain. The player attached one thing and got another,
    /// with no way to work out where the second one came from.</para>
    ///
    /// <para>The phases are spread by attachment order, so a left/right pair gets antiphase and the
    /// creature does not hop on both legs at once.</para>
    /// </remarks>
    public static class CreatureLegFactory
    {
        /// <summary>
        /// Builds legs from <paramref name="body"/>'s locomotion parts and writes them into
        /// <paramref name="legs"/>, clearing the list's previous contents.
        /// </summary>
        public static void Build(CreatureGenome genome, PartRuleSet rules, BuiltCreatureBody body, List<ProceduralLeg> legs)
        {
            legs.Clear();

            if (genome == null || body?.PartInstances == null) return;

            var hips = new List<(Transform hip, LegSpec spec, int geneIndex)>();

            foreach (CreaturePartInstance instance in body.PartInstances)
            {
                if (instance.Object == null) continue;

                // The leg is resolved exactly the way the stats resolve it: the player's reshaping
                // takes precedence over the catalog.
                if (!GenomeStatRules.TryResolveLeg(genome, rules, instance.GeneIndex, out LegSpec leg, out _)) continue;

                // The part instance becomes the hip: it sits on the bone, so the chain grows straight
                // out of the attachment point and travels with the carcass.
                hips.Add((instance.Object.transform, leg, instance.GeneIndex));
            }

            for (int i = 0; i < hips.Count; i++)
                legs.Add(new ProceduralLeg(hips[i].spec, hips[i].hip, GaitProfile.PhaseOffset(i, hips.Count), hips[i].geneIndex));
        }

        /// <summary>Tears down the legs and gives the parts their authored visuals back.</summary>
        public static void Dispose(List<ProceduralLeg> legs)
        {
            foreach (ProceduralLeg leg in legs) leg.Dispose();
            legs.Clear();
        }
    }
}
