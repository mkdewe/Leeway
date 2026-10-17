namespace Leeway.Creature.Domain
{
    /// <summary>What one genome costs, measured against the available currency.</summary>
    public readonly struct BudgetReport
    {
        public readonly int Spent;
        public readonly int Budget;

        public BudgetReport(int spent, int budget)
        {
            Spent = spent;
            Budget = budget;
        }

        public int Remaining => Budget - Spent;
        public bool IsAffordable => Spent <= Budget;

        public override string ToString() => $"{Spent}/{Budget}";
    }

    /// <summary>
    /// Prices a creature: vertebrae at a flat rate, parts at the price from their own rule.
    /// </summary>
    /// <remarks>
    /// This is the <b>only</b> size limit the player ever sees — the counts in
    /// <see cref="GenomeLimits"/> exist purely so the genome fits into the network blob.
    /// Both the client (live readout) and the server (at commit time) compute it, each
    /// from <b>its own</b> rule set.
    /// </remarks>
    public static class CreatureBudget
    {
        public static BudgetReport Evaluate(CreatureGenome genome, PartRuleSet rules)
        {
            if (rules == null) rules = PartRuleSet.Empty;
            if (genome == null) return new BudgetReport(0, rules.Budget);

            int spent = genome.VertebraCount * rules.VertebraCost;

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);

                // A part outside the catalog has no price; reporting that it is unknown is the
                // validator's job, not the pricing's — here it simply costs nothing.
                if (rules.TryGetRule(gene.PartId, out PartRule rule))
                    spent += rule.Cost;

                // What is fitted into a limb is bought as well. A hoof is a part the player chose,
                // and one leg with an expensive hand on it must not cost the same as a bare stump.
                if (gene.HasFitting && rules.TryGetRule(gene.FittingId, out PartRule fitting))
                    spent += fitting.Cost;
            }

            return new BudgetReport(spent, rules.Budget);
        }

        /// <summary>Whether the genome can still absorb a further spend of <paramref name="additionalCost"/>.</summary>
        public static bool CanAfford(CreatureGenome genome, PartRuleSet rules, int additionalCost)
        {
            BudgetReport report = Evaluate(genome, rules);
            return report.Spent + additionalCost <= report.Budget;
        }
    }
}
