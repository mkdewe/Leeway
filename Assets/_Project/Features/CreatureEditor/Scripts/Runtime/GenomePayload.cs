using System;
using FishNet.CodeGenerating;
using Leeway.Creature.Domain;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The genome in transport form: the encoded blob plus its hash.
    /// </summary>
    /// <remarks>
    /// <see cref="IEquatable{T}"/> is not decoration — <c>SyncVar&lt;T&gt;</c> detects a change
    /// through <c>EqualityComparer&lt;T&gt;.Default</c>, and the default comparison of a struct
    /// containing an array compares it <b>by reference</b>, so every write would count as a change.
    /// The hash gives an O(1) comparison and doubles as the idempotency key for rebuilding the body.
    /// </remarks>
    [UseGlobalCustomSerializer]
    public readonly struct GenomePayload : IEquatable<GenomePayload>
    {
        public readonly byte[] Blob;
        public readonly uint Hash;

        public GenomePayload(byte[] blob)
        {
            Blob = blob;
            Hash = StableHash.Fnv1a32(blob);
        }

        public bool IsEmpty => Blob == null || Blob.Length == 0;

        public static GenomePayload FromGenome(CreatureGenome genome) => new GenomePayload(GenomeCodec.Encode(genome));

        public bool TryDecode(out CreatureGenome genome, out GenomeError error)
            => GenomeCodec.TryDecode(Blob, out genome, out error);

        public bool Equals(GenomePayload other)
            => Hash == other.Hash && (Blob?.Length ?? 0) == (other.Blob?.Length ?? 0);

        public override bool Equals(object obj) => obj is GenomePayload other && Equals(other);
        public override int GetHashCode() => unchecked((int)Hash);
    }
}
