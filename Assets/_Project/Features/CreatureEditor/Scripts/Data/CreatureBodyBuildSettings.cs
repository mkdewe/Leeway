using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Parameters for generating the body. Shared by client and server — both sides have to build
    /// from the same settings, otherwise the colliders drift away from what is on screen.
    /// </summary>
    [CreateAssetMenu(fileName = "BodyBuildSettings", menuName = "Leeway/Creature/Body Build Settings")]
    public class CreatureBodyBuildSettings : ScriptableObject
    {
        [Header("Mesh")]
        [Tooltip("How many rings there are per spine segment. Higher = smoother, but more vertices.")]
        [field: SerializeField, Range(1, 6)] public int RingsPerSegment { get; private set; } = 3;

        [Tooltip("Number of segments around a ring's circumference.")]
        [field: SerializeField, Range(4, 24)] public int RadialSegments { get; private set; } = 8;

        [Tooltip("How many hoops the dome closing the head and tail has. The dome is a hemisphere with " +
                 "the end vertebra's radius - the tip length is not a choice, because PartPlacement " +
                 "measures the body as a capsule and parts have to sit on that same skin.")]
        [field: SerializeField, Range(1, 6)] public int CapRings { get; private set; } = 3;

        [Header("Rendering")]
        [field: SerializeField] public Material BodyMaterial { get; private set; }

        [Tooltip("The colours and coat patterns a creature can be painted with. Left empty: creatures come out in flat colour, with no markings.")]
        [field: SerializeField] public CreatureSkinPalette SkinPalette { get; private set; }

        [Tooltip("Padding for the explicit SkinnedMeshRenderer bounds. The default bounds would cull the creature mid-ragdoll.")]
        [field: SerializeField] public float BoundsPadding { get; private set; } = 1.5f;

        [Header("Physics")]
        [Tooltip("Minimum radius of the locomotion capsule, so a thin creature does not fall through the floor.")]
        [field: SerializeField] public float MinColliderRadius { get; private set; } = 0.15f;
    }
}
