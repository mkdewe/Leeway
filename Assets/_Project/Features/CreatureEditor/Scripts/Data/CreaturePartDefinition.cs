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
                 "the step reach and how many links the generated leg chain has.\n\n" +
                 "Unless Whole Limb is ticked, the prefab is ONE link and the chain repeats it for " +
                 "every further one - a model holding a complete leg would then appear twice over, " +
                 "with a false knee where the copy starts.")]
        [field: SerializeField, Range(LegLimits.MinBendPoints, LegLimits.MaxBendPoints)]
        public int BendPoints { get; private set; } = 2;

        [Tooltip("Tick when the model is the WHOLE leg - thigh, shank and foot in one mesh.\n\n" +
                 "The chain then skins that single model over its links instead of repeating it, and " +
                 "bends it at the knee the model itself is drawn with. Segment Length is then the " +
                 "length of one link, i.e. the model's reach divided by the number of links.")]
        [field: SerializeField] public bool WholeLimb { get; private set; }

        [Tooltip("The length of one leg link.")]
        [field: SerializeField, Range(LegLimits.MinSegmentLength, LegLimits.MaxSegmentLength)]
        public float SegmentLength { get; private set; } = 0.22f;

        [Header("Socket")]
        [Tooltip("Tick for a limb that ends in an ankle or a wrist: the player then fits a foot or a " +
                 "hand of their own choosing into it.\n\n" +
                 "The prefab needs a child named \"Socket\" where that part goes - its +Z is the " +
                 "direction the foot points.")]
        [field: SerializeField] public bool AcceptsFitting { get; private set; }

        [Tooltip("What sits in the socket unless the player picks otherwise. A limb attached from the " +
                 "palette comes with this already fitted, so nobody ends up with a creature on stumps.")]
        [field: SerializeField] public CreaturePartDefinition DefaultFitting { get; private set; }

        /// <summary>The name of the child transform a fitted part is seated on.</summary>
        public const string SocketName = "Socket";

        [Header("Cost")]
        [Tooltip("What attaching this part costs. A mirrored pair costs the same as a single piece.")]
        [field: SerializeField, Min(0)] public int Cost { get; private set; } = 25;

        // Cached in OnValidate so the hash is not recomputed on every read at runtime.
        [SerializeField, HideInInspector] private int _cachedPartId;

        /// <summary>The stable identifier used in the genome.</summary>
        public int PartId => _cachedPartId != 0 ? _cachedPartId : StableHash.PartId(PartKey);

        /// <summary>Projects the definition onto a pure domain rule — the bridge between the asset and the testable layer.</summary>
        public PartRule ToRule()
            => new PartRule(PartId, Category, AllowedSites, MirrorCapable, Stats, Cost, Leg, SkinOffset, WholeLimb,
                AcceptsFitting, DefaultFitting != null ? DefaultFitting.PartId : 0);

        /// <summary>The leg description derived from this definition. Only meaningful for locomotion parts.</summary>
        public LegSpec Leg => new LegSpec(BendPoints, SegmentLength);

        private void OnValidate()
        {
            _cachedPartId = StableHash.PartId(PartKey);

            if (string.IsNullOrWhiteSpace(PartKey))
                Debug.LogError($"[{name}] PartKey is empty — this part cannot be addressed from a genome.", this);

            // An extremity is meant to have nowhere on the spine to go: it is fitted into a limb's
            // socket, and a hoof that could also be stuck on the ribs is a bug, not a feature.
            if (AllowedSites == AttachmentSite.None && Category != PartCategory.Extremity)
                Debug.LogWarning($"[{name}] AllowedSites = None — this part cannot be attached anywhere.", this);
        }
    }
}
