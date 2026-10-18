using System.Collections.Generic;

namespace Leeway.World.Domain
{
    /// <summary>The plants a creature can eat. Scenery that only decorates (grass, ferns, boulders) is not in here.</summary>
    public enum FloraKind : byte { BerryBush, FruitTree, Mushroom, Herb, DeathCap }

    /// <summary>What one kind of edible plant gives, how fast it grows back, and where it grows.</summary>
    public readonly struct FloraSpec
    {
        public readonly FloraKind Kind;

        /// <summary>The plant's name as the player sees it.</summary>
        public readonly string Name;

        /// <summary>What is picked from it, for the notice: "jagody", "owoce".</summary>
        public readonly string Fruit;

        /// <summary>How much of hunger one picking satisfies. Negative is a poison that makes it worse.</summary>
        public readonly float Satiety;

        /// <summary>Hit points restored (negative: hit points lost).</summary>
        public readonly float Health;

        /// <summary>Seconds until the plant has fruit again.</summary>
        public readonly float RegrowSeconds;

        /// <summary>How far from the plant a creature can reach it, in metres.</summary>
        public readonly float Reach;

        public readonly ScatterRule Rule;

        public FloraSpec(FloraKind kind, string name, string fruit, float satiety, float health, float regrowSeconds, float reach, ScatterRule rule)
        {
            Kind = kind;
            Name = name;
            Fruit = fruit;
            Satiety = satiety;
            Health = health;
            RegrowSeconds = regrowSeconds;
            Reach = reach;
            Rule = rule;
        }
    }

    /// <summary>
    /// The edible flora of an island, as data: what each plant gives and where it may grow. The server and every
    /// client read the same table, and the same seed puts every plant in the same place on every machine - only
    /// <i>which</i> plants have been picked clean has to be sent.
    /// </summary>
    public static class FloraRules
    {
        private static readonly FloraSpec[] Table =
        {
            //                         kind                    name                      fruit      sat  hp   regrow reach  density, minH, maxH, slope, hub, cluster, clusterScale, minScale, maxScale
            new(FloraKind.BerryBush, "Krzew jagodowy",   "jagody",   14f,  3f,  75f, 2.4f, new ScatterRule(0.34f, 0.8f, 14f, 28f, 6f, 0.45f, 38f, 0.9f, 1.3f)),
            new(FloraKind.FruitTree, "Drzewo owocowe",   "owoce",    26f,  6f, 140f, 3.2f, new ScatterRule(0.12f, 1.0f, 12f, 22f, 10f, 0.30f, 60f, 0.9f, 1.2f)),
            new(FloraKind.Mushroom,  "Grzyby",           "grzyby",    9f,  0f, 100f, 2.0f, new ScatterRule(0.25f, 0.5f, 10f, 30f, 6f, 0.55f, 30f, 0.8f, 1.2f)),
            new(FloraKind.Herb,      "Ziele lecznicze",  "ziele",     2f, 20f, 160f, 2.0f, new ScatterRule(0.15f, 0.6f, 9f, 30f, 6f, 0.50f, 34f, 0.8f, 1.2f)),
            new(FloraKind.DeathCap,  "Muchomor",         "muchomora", 4f, -12f, 120f, 2.0f, new ScatterRule(0.10f, 0.5f, 10f, 30f, 8f, 0.60f, 26f, 0.8f, 1.2f)),
        };

        public static IReadOnlyList<FloraSpec> Specs => Table;

        public static FloraSpec Of(FloraKind kind) => Table[(int)kind];

        /// <summary>The scatter rules in the same order as <see cref="Specs"/>, for <see cref="IslandScatter.Plan"/>.</summary>
        public static List<ScatterRule> Rules()
        {
            var rules = new List<ScatterRule>(Table.Length);
            foreach (FloraSpec spec in Table) rules.Add(spec.Rule);
            return rules;
        }

        /// <summary>One variant of each: the models differ by kind, not by variant.</summary>
        public static List<int> VariantCounts()
        {
            var counts = new List<int>(Table.Length);
            for (int i = 0; i < Table.Length; i++) counts.Add(1);
            return counts;
        }
    }
}
