namespace Leeway.Creature.Domain
{
    /// <summary>
    /// FNV-1a 32-bit. Used for part identifiers and for the genome hash.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> <c>string.GetHashCode()</c>: .NET string hashing is
    /// randomised per process and differs between platforms, so part identifiers
    /// would stop matching between client and server.
    /// </remarks>
    public static class StableHash
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;

        /// <summary>
        /// Hashes the UTF-8 bytes of the text. Encoding explicitly (rather than iterating
        /// over <c>char</c>) matches the published FNV-1a test vectors, so stability can be
        /// pinned by a test instead of merely asserted.
        /// </summary>
        public static uint Fnv1a32(string value)
        {
            if (string.IsNullOrEmpty(value)) return OffsetBasis;
            return Fnv1a32(System.Text.Encoding.UTF8.GetBytes(value));
        }

        public static uint Fnv1a32(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return OffsetBasis;

            uint hash = OffsetBasis;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * Prime;
            return hash;
        }

        /// <summary>Variant for part identifiers — <see cref="PartGene.PartId"/> is an int.</summary>
        public static int PartId(string partKey) => unchecked((int)Fnv1a32(partKey));
    }
}
