using UnityEngine;

namespace Leeway.Creature
{
    [CreateAssetMenu(fileName = "CreatureEntityConfig", menuName = "Leeway/Creature Entity Config")]
    public class CreatureEntityConfig : ScriptableObject
    {
        [Header("Movement")]
        [field: SerializeField] public float BaseSpeed { get; private set; } = 4f;
        [field: SerializeField] public float Drag { get; private set; } = 6f;
        [field: SerializeField] public float RotationSpeed { get; private set; } = 12f;

        [Header("Health")]
        [field: SerializeField] public float BaseHp { get; private set; } = 100f;

        [Header("Combat")]
        [field: SerializeField] public float AttackDamage { get; private set; } = 15f;
        [field: SerializeField] public float AttackRange { get; private set; } = 1.5f;
        [field: SerializeField] public float AttackCooldown { get; private set; } = 1f;

        [Header("Food")]
        [field: SerializeField] public float MaxFood { get; private set; } = 100f;

        [Header("AI")]
        [Tooltip("How far an NPC flees from a threat.")]
        [field: SerializeField] public float FleeDistance { get; private set; } = 6f;
    }
}
