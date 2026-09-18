using FishNet.Serializing;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// A hand-written <see cref="GenomePayload"/> serializer for FishNet.
    /// </summary>
    /// <remarks>
    /// FishNet's codegen finds serializers by convention: an extension method whose name starts with
    /// <c>Write</c>/<c>Read</c> and whose first parameter is a <c>Writer</c>/<c>Reader</c>. FishNet's
    /// ILPostProcessor handles every assembly that references <c>FishNet.Runtime</c>, so this class
    /// works the same in <c>Leeway.CreatureEditor</c> as it would in <c>Assembly-CSharp</c>.
    ///
    /// We write and check the length ourselves instead of using <c>WriteUInt8ArrayAndSize</c>:
    /// <b>the range check has to happen before the allocation</b>. Without it, a client declaring a
    /// two-gigabyte array takes the server down on out-of-memory before genome validation even starts.
    /// </remarks>
    public static class CreatureGenomeNetworkSerializer
    {
        private const int NullLength = -1;

        public static void WriteGenomePayload(this Writer writer, GenomePayload value)
        {
            byte[] blob = value.Blob;
            if (blob == null)
            {
                writer.WriteInt32(NullLength);
                return;
            }

            writer.WriteInt32(blob.Length);
            writer.WriteUInt8Array(blob, 0, blob.Length);
        }

        public static GenomePayload ReadGenomePayload(this Reader reader)
        {
            int length = reader.ReadInt32();

            if (length == NullLength) return default;

            if (length < 0 || length > GenomeLimits.MaxBlobBytes)
            {
                // The stream is doctored or corrupt — there is nothing in it worth salvaging.
                Debug.LogWarning($"Rejected a genome with a declared length of {length} B (limit {GenomeLimits.MaxBlobBytes} B).");
                reader.Clear();
                return default;
            }

            byte[] blob = reader.ReadUInt8ArrayAllocated(length);
            return new GenomePayload(blob);
        }
    }
}
