using System;
using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>One vertebra inside a saved creature.</summary>
    [Serializable]
    public struct VertebraRecord
    {
        [SerializeField] private Vector3 _offset;
        [SerializeField] private Quaternion _rotation;
        [SerializeField] private float _radius;

        public VertebraRecord(Vector3 offset, Quaternion rotation, float radius)
        {
            _offset = offset;
            _rotation = rotation;
            _radius = radius;
        }

        public static VertebraRecord From(in VertebraGene gene)
            => new VertebraRecord(gene.LocalOffset, gene.LocalRotation, gene.Radius);

        public VertebraGene ToGene() => new VertebraGene(_offset, _rotation, _radius);
    }

    /// <summary>One attached part inside a saved creature.</summary>
    /// <remarks>
    /// It carries <b>both</b> the catalog key and the identifier. The identifier is what the genome
    /// actually runs on, and the key is what a human — or a tool that is not this build — can read and
    /// edit: <c>"loco.hoof_legs"</c> says what the part is, <c>-179235692</c> does not. On the way back
    /// in the key wins whenever it is filled in, because it re-hashes to the identifier anyway
    /// (<see cref="StableHash.PartId"/>), so a hand-written preset needs the key alone.
    /// </remarks>
    [Serializable]
    public struct PartRecord
    {
        [SerializeField] private string _key;
        [SerializeField] private int _id;
        [SerializeField] private int _bone;
        [SerializeField] private Vector3 _position;
        [SerializeField] private Quaternion _rotation;
        [SerializeField] private float _scale;
        [SerializeField] private bool _mirrored;
        [SerializeField] private bool _hasLegOverride;
        [SerializeField] private int _bendPoints;
        [SerializeField] private float _segmentLength;

        /// <summary>The part's own colour. Absent from the file, or an alpha of 0, means "never painted".</summary>
        [SerializeField] private Color32 _tint;

        [SerializeField] private int _pattern;

        /// <summary>What is fitted into this part's socket, by catalog key — the readable half, like <see cref="_key"/>.</summary>
        [SerializeField] private string _fittingKey;

        /// <summary>The fitted part's identifier, for a file written without the key.</summary>
        [SerializeField] private int _fitting;

        public PartRecord(string key, int id, int bone, Vector3 position, Quaternion rotation, float scale,
            bool mirrored, bool hasLegOverride, int bendPoints, float segmentLength, Color32 tint, int pattern,
            string fittingKey = null, int fitting = 0)
            : this(key, id, bone, position, rotation, scale, mirrored, hasLegOverride, bendPoints, segmentLength)
        {
            _tint = tint;
            _pattern = pattern;
            _fittingKey = fittingKey;
            _fitting = fitting;
        }

        public PartRecord(string key, int id, int bone, Vector3 position, Quaternion rotation, float scale,
            bool mirrored, bool hasLegOverride, int bendPoints, float segmentLength)
        {
            _tint = default;
            _pattern = 0;
            _fittingKey = null;
            _fitting = 0;
            _key = key;
            _id = id;
            _bone = bone;
            _position = position;
            _rotation = rotation;
            _scale = scale;
            _mirrored = mirrored;
            _hasLegOverride = hasLegOverride;
            _bendPoints = bendPoints;
            _segmentLength = segmentLength;
        }

        /// <summary>The catalog key, when the saver knew it. Empty is legal — the identifier still works.</summary>
        public string Key => _key;

        /// <summary>The identifier the genome addresses the part by: the key's hash when there is a key.</summary>
        public int PartId => string.IsNullOrEmpty(_key) ? _id : StableHash.PartId(_key);

        /// <summary>The identifier of the fitted part, or zero when the limb ends in a stump.</summary>
        public int FittingId => string.IsNullOrEmpty(_fittingKey) ? _fitting : StableHash.PartId(_fittingKey);

        public static PartRecord From(in PartGene gene, string key, string fittingKey = null)
            => new PartRecord(key, gene.PartId, gene.BoneIndex, gene.LocalPosition, gene.LocalRotation, gene.Scale,
                gene.Mirrored, gene.HasLegOverride, gene.Leg.BendPoints, gene.Leg.SegmentLength,
                gene.Tint, gene.PatternId, fittingKey, gene.FittingId);

        public PartGene ToGene()
            => new PartGene(PartId, (byte)Mathf.Clamp(_bone, 0, byte.MaxValue), _position, _rotation, _scale,
                _mirrored, _hasLegOverride, new LegSpec(_bendPoints, _segmentLength),
                _tint, (byte)Mathf.Clamp(_pattern, 0, byte.MaxValue), FittingId);
    }

    /// <summary>
    /// A creature saved outside the game: the genome in a form that survives a round trip through a
    /// file and can be read by something that is not this build.
    /// </summary>
    /// <remarks>
    /// <para><b>Why not <see cref="GenomeCodec"/>.</b> The codec exists for the wire — quantised,
    /// size-capped, a blob. That is the right shape for a packet and the wrong one for a preset:
    /// nobody can read it, diff it in a commit, or write one by hand. This is the other half of the
    /// pair — plain fields, lossless, <c>JsonUtility</c>-shaped. The genome itself remains the single
    /// representation both are made from.</para>
    ///
    /// <para><b>The fields are serialised private ones</b>, as everywhere else in the project, so the
    /// JSON keys carry the leading underscore (<c>"_vertebrae"</c>, <c>"_parts"</c>). That is a naming
    /// detail of the file, not of the format: any JSON reader maps it in one line, and Unity reads it
    /// back into this very type for nothing.</para>
    ///
    /// <para>Versioned like the codec, and for the same reason: a preset saved today has to be
    /// readable by a build from a month's time, and one saved by a <b>newer</b> build must be refused
    /// out loud rather than loaded as a creature missing half of itself.</para>
    /// </remarks>
    [Serializable]
    public class GenomeDocument
    {
        public const int CurrentVersion = 1;

        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private string _name;
        [SerializeField] private Color32 _primaryColor = new Color32(120, 180, 110, 255);
        [SerializeField] private Color32 _secondaryColor = new Color32(60, 90, 60, 255);
        [SerializeField] private int _bodyPattern;
        [SerializeField] private VertebraRecord[] _vertebrae = Array.Empty<VertebraRecord>();
        [SerializeField] private PartRecord[] _parts = Array.Empty<PartRecord>();
        [SerializeField] private string _skin;

        public int Version => _version;

        /// <summary>What the player called this creature. Decoration — nothing resolves by it.</summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public int VertebraCount => _vertebrae?.Length ?? 0;
        public int PartCount => _parts?.Length ?? 0;

        /// <summary>
        /// The painted skin, as a base64 PNG. Empty when the creature was never painted.
        /// </summary>
        /// <remarks>
        /// <para>Since painting moved onto Paint in 3D, what a creature looks like is no longer a pair
        /// of numbers in the genome but a texture — so a preset that did not carry the texture would
        /// restore a creature wearing only its base coat, with every stroke gone.</para>
        ///
        /// <para>It is the one field here that is <b>not</b> small: a 512² skin runs to tens of
        /// kilobytes against the genome's few hundred bytes. That is the price of free painting, and it
        /// is why the field is optional — a creature with no strokes on it still writes a preset the
        /// size it always was.</para>
        /// </remarks>
        public string Skin
        {
            get => _skin;
            set => _skin = value;
        }

        /// <summary>
        /// Captures a genome.
        /// </summary>
        /// <param name="keyOf">
        /// Resolves a part identifier to its catalog key, so the file says what the parts are. May be
        /// <c>null</c> — the document then holds bare identifiers, which still load.
        /// </param>
        public static GenomeDocument From(CreatureGenome genome, string name = null, Func<int, string> keyOf = null)
        {
            var document = new GenomeDocument { _version = CurrentVersion, _name = name };
            if (genome == null) return document;

            document._primaryColor = genome.PrimaryColor;
            document._secondaryColor = genome.SecondaryColor;
            document._bodyPattern = genome.BodyPattern;

            document._vertebrae = new VertebraRecord[genome.VertebraCount];
            for (int i = 0; i < genome.VertebraCount; i++)
                document._vertebrae[i] = VertebraRecord.From(genome.GetVertebra(i));

            document._parts = new PartRecord[genome.PartCount];
            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene gene = genome.GetPart(i);
                document._parts[i] = PartRecord.From(gene, keyOf?.Invoke(gene.PartId),
                    gene.HasFitting ? keyOf?.Invoke(gene.FittingId) : null);
            }

            return document;
        }

        /// <summary>
        /// Rebuilds the genome.
        /// </summary>
        /// <remarks>
        /// Deliberately without validating: a document is raw input, and the rules belong to
        /// <see cref="GenomeValidator"/> — the same gate every other genome goes through, the one the
        /// server runs too. A preset from a file gets no shortcut past it.
        /// </remarks>
        public CreatureGenome ToGenome()
        {
            var genome = new CreatureGenome
            {
                PrimaryColor = _primaryColor,
                SecondaryColor = _secondaryColor,
                BodyPattern = (byte)Mathf.Clamp(_bodyPattern, 0, byte.MaxValue),
            };

            if (_vertebrae != null)
                foreach (VertebraRecord vertebra in _vertebrae) genome.AddVertebra(vertebra.ToGene());

            if (_parts != null)
                foreach (PartRecord part in _parts) genome.AddPart(part.ToGene());

            return genome;
        }

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        /// <summary>
        /// Reads a document from JSON.
        /// </summary>
        /// <remarks>
        /// Everything that can go wrong with a file the player may have edited by hand comes back as
        /// <paramref name="error"/> rather than as an exception: a preset folder is not a trusted
        /// input, and a broken file must cost a message, not the session.
        /// </remarks>
        public static bool TryFromJson(string json, out GenomeDocument document, out string error)
        {
            document = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The file is empty.";
                return false;
            }

            try
            {
                document = JsonUtility.FromJson<GenomeDocument>(json);
            }
            catch (Exception exception)
            {
                error = $"This is not a creature preset: {exception.Message}";
                return false;
            }

            if (document == null)
            {
                error = "This is not a creature preset.";
                return false;
            }

            if (document._version > CurrentVersion)
            {
                error = $"The preset was saved by a newer version of the game (format {document._version}, this build reads {CurrentVersion}).";
                document = null;
                return false;
            }

            return true;
        }
    }
}
