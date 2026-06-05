using UnityEngine;

namespace Leeway.Creature
{
    [CreateAssetMenu(fileName = "CellEntityConfig", menuName = "Leeway/Cell Entity Config")]
    public class CellEntityConfig : ScriptableObject
    {
        [field: SerializeField] public float BaseSpeed { get; private set; } = 5f;
        [field: SerializeField] public float MaxSpeed { get; private set; } = 12f;
        [field: SerializeField] public float BaseHp { get; private set; } = 100f;
        [field: SerializeField] public float BaseSize { get; private set; } = 1f;
        [field: SerializeField] public float MaxSize { get; private set; } = 8f;
        [field: SerializeField] public float Drag { get; private set; } = 3f;
        [field: SerializeField] public float EatSizeRatio { get; private set; } = 1.15f;
        [field: SerializeField] public float GrowthPerEat { get; private set; } = 0.25f;

        [Header("Ruch")]
        [Tooltip("Dolny limit prędkości jako ułamek BaseSpeed (gdy komórka jest duża).")]
        [field: SerializeField] public float SpeedFloorRatio { get; private set; } = 0.3f;

        [Header("AI")]
        [Tooltip("Jak daleko NPC ucieka od zagrożenia.")]
        [field: SerializeField] public float FleeDistance { get; private set; } = 8f;
    }
}
