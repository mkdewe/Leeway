namespace Leeway.Creature.Domain
{
    /// <summary>
    /// What one part contributes to the creature's stats. A plain struct — it lives in
    /// the domain so <see cref="GenomeStatRules"/> never has to see a ScriptableObject.
    /// </summary>
    [System.Serializable]
    public struct PartStatContribution
    {
        public float HpBonus;
        public float SpeedBonus;
        public float DamageBonus;
        public float SenseRadiusBonus;
        public float MassKg;

        public static PartStatContribution operator +(PartStatContribution a, PartStatContribution b) => new PartStatContribution
        {
            HpBonus = a.HpBonus + b.HpBonus,
            SpeedBonus = a.SpeedBonus + b.SpeedBonus,
            DamageBonus = a.DamageBonus + b.DamageBonus,
            SenseRadiusBonus = a.SenseRadiusBonus + b.SenseRadiusBonus,
            MassKg = a.MassKg + b.MassKg,
        };
    }
}
