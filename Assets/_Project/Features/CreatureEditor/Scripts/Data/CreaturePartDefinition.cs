using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The authored definition of one body part. The genome refers to it through
    /// <see cref="PartId"/> — the stable hash of <see cref="PartKey"/> — never through an asset
    /// reference, because the genome travels over the network and has to resolve against the
    /// receiver's catalog.
    /// </summary>
    [CreateAssetMenu(fileName = "PartDefinition", menuName = "Leeway/Creature/Part Definition")]
    public class CreaturePartDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The stable authored key, e.g. \"loco.stub_legs\". Changing the key invalidates every saved genome that uses this part.")]
        [field: SerializeField] public string PartKey { get; private set; } = "category.name";

        [field: SerializeField] public string DisplayName { get; private set; } = "Part";

        [Header("Rules")]
        [field: SerializeField] public PartCategory Category { get; private set; } = PartCategory.Sense;
        [field: SerializeField] public AttachmentSite AllowedSites { get; private set; } = AttachmentSite.Any;

        [Tooltip("Whether the part makes sense as a pair (legs, eyes). The mirroring is done by the instantiator, not by a separate definition.")]
        [field: SerializeField] public bool MirrorCapable { get; private set; }

        [Header("Visuals")]
        [field: SerializeField] public GameObject Prefab { get; private set; }

        [Tooltip("How far the part lifts above the skin. A negative value sinks it into the body. " +
                 "The point on the skin itself is computed by PartPlacement from the vertebra radii - this is only a top-up.")]
        [field: SerializeField] public float SkinOffset { get; private set; }

        [Header("Stats")]
        [field: SerializeField] public PartStatContribution Stats { get; private set; }

        [Header("Leg")]
        [Tooltip("Bend points. Applies to locomotion parts only - it decides the gait style, " +
                 "the step reach and how many links the generated leg chain has.")]
        [field: SerializeField, Range(LegLimits.MinBendPoints, LegLimits.MaxBendPoints)]
        public int BendPoints { get; private set; } = 2;

        [Tooltip("The length of one leg link.")]
        [field: SerializeField, Range(LegLimits.MinSegmentLength, LegLimits.MaxSegmentLength)]
        public float SegmentLength { get; private set; } = 0.22f;

        [Header("Cost")]
        [Tooltip("What attaching this part costs. A mirrored pair costs the same as a single piece.")]
        [field: SerializeField, Min(0)] public int Cost { get; private set; } = 25;

        // Cached in OnValidate so the hash is not recomputed on every read at runtime.
        [SerializeField, HideInInspector] private int _cachedPartId;

        /// <summary>The stable identifier used in the genome.</summary>
        public int PartId => _cachedPartId != 0 ? _cachedPartId : StableHash.PartId(PartKey);

        /// <summary>Projects the definition onto a pure domain rule — the bridge between the asset and the testable layer.</summary>
        public PartRule ToRule() => new PartRule(PartId, Category, AllowedSites, MirrorCapable, Stats, Cost, Leg, SkinOffset);

        /// <summary>The leg description derived from this definition. Only meaningful for locomotion parts.</summary>
        public LegSpec Leg => new LegSpec(BendPoints, SegmentLength);

        private void OnValidate()
        {
            _cachedPartId = StableHash.PartId(PartKey);

            if (string.IsNullOrWhiteSpace(PartKey))
                Debug.LogError($"[{name}] PartKey is empty — this part cannot be addressed from a genome.", this);

            if (AllowedSites == AttachmentSite.None)
                Debug.LogWarning($"[{name}] AllowedSites = None — this part cannot be attached anywhere.", this);
        }
    }
}
