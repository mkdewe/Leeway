using UnityEngine;

namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Turns a genome into a versioned byte blob and back.
    /// </summary>
    /// <remarks>
    /// The format is hand-rolled rather than built on FishNet's auto-serializer, because it buys four
    /// things the auto-serializer does not: version tolerance, quantisation, a hard size cap and a
    /// cheap content hash. The encoding is explicitly little-endian so it does not depend on the platform.
    ///
    /// Layout (version 1):
    ///   [0]      magic 0x4C
    ///   [1]      version
    ///   [2]      vertebra count
    ///   [3]      part count
    ///   [4..6]   primary colour RGB24
    ///   [7..9]   secondary colour RGB24
    ///   then 14 B per vertebra and 20 B per part.
    ///
    /// Version 2 adds 3 B of reshaped leg (bend points + segment length) to the part record. A
    /// version 1 genome still loads — its parts then get the catalog's leg, which is exactly what they
    /// meant before the recording was introduced.
    ///
    /// Version 3 adds the paintwork: 1 B of body pattern to the header, and 3 B of colour plus 1 B of
    /// pattern to the part record. Older genomes still load — their parts come out unpainted, which is
    /// what they were: the creature's secondary colour and bare skin.
    ///
    /// Version 4 adds 4 B for the part fitted into this one's socket — the foot on a leg, the hand on
    /// an arm. Older genomes still load with nothing fitted, which is what they had: limbs whose foot
    /// was part of the limb's own model.
    /// </remarks>
    public static class GenomeCodec
    {
        public const byte Magic = 0x4C;
        public const byte Version = 4;
        public const byte MinSupportedVersion = 1;

        /// <summary>The size of the header in the current format version.</summary>
        public const int HeaderBytes = 11;

        /// <summary>The size of the header in versions 1 and 2 — without the body pattern.</summary>
        public const int HeaderBytesV1 = 10;

        public const int VertebraBytes = 14;

        /// <summary>The size of a part record in the current format version.</summary>
        public const int PartBytes = 31;

        /// <summary>The size of a part record in version 1 — without the reshaped leg.</summary>
        public const int PartBytesV1 = 20;

        /// <summary>The size of a part record in version 2 — with the leg, without the paintwork.</summary>
        public const int PartBytesV2 = 23;

        /// <summary>The size of a part record in version 3 — with the paintwork, without the fitted part.</summary>
        public const int PartBytesV3 = 27;

        private const float PositionScale = 1000f;   // 1/1000 m
        private const float AngleScale = 100f;       // 1/100 degree
        private const float RadiusScale = 1000f;     // 1/1000 m
        private const float PartScaleScale = 1000f;  // 1/1000
        private const float SegmentLengthScale = 1000f; // 1/1000 m

        private const byte FlagMirrored = 1 << 0;
        private const byte FlagLegOverride = 1 << 1;

        /// <summary>Whether the part carries a colour of its own. Without it the three colour bytes mean nothing.</summary>
        private const byte FlagTinted = 1 << 2;

        public static int ComputeSize(CreatureGenome genome)
        {
            if (genome == null) return 0;
            return HeaderBytes + genome.VertebraCount * VertebraBytes + genome.PartCount * PartBytes;
        }

        public static byte[] Encode(CreatureGenome genome)
        {
            if (genome == null) return new byte[0];

            var buffer = new byte[ComputeSize(genome)];
            int offset = 0;

            buffer[offset++] = Magic;
            buffer[offset++] = Version;
            buffer[offset++] = (byte)genome.VertebraCount;
            buffer[offset++] = (byte)genome.PartCount;

            Color32 primary = genome.PrimaryColor;
            buffer[offset++] = primary.r;
            buffer[offset++] = primary.g;
            buffer[offset++] = primary.b;

            Color32 secondary = genome.SecondaryColor;
            buffer[offset++] = secondary.r;
            buffer[offset++] = secondary.g;
            buffer[offset++] = secondary.b;

            buffer[offset++] = genome.BodyPattern;

            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene v = genome.GetVertebra(i);
                WriteVector(buffer, ref offset, v.LocalOffset, PositionScale);
                WriteEuler(buffer, ref offset, v.LocalRotation);
                WriteUInt16(buffer, ref offset, QuantizeUnsigned(v.Radius, RadiusScale));
            }

            for (int i = 0; i < genome.PartCount; i++)
            {
                PartGene p = genome.GetPart(i);
                WriteInt32(buffer, ref offset, p.PartId);
                buffer[offset++] = p.BoneIndex;
                WriteVector(buffer, ref offset, p.LocalPosition, PositionScale);
                WriteEuler(buffer, ref offset, p.LocalRotation);
                WriteUInt16(buffer, ref offset, QuantizeUnsigned(p.Scale, PartScaleScale));

                byte flags = 0;
                if (p.Mirrored) flags |= FlagMirrored;
                if (p.HasLegOverride) flags |= FlagLegOverride;
                if (p.HasTint) flags |= FlagTinted;
                buffer[offset++] = flags;

                // The leg is always written — the record has a fixed length, so the blob size can be
                // computed without walking the parts. Whether those bytes mean anything is decided
                // solely by the flag.
                buffer[offset++] = (byte)p.Leg.BendPoints;
                WriteUInt16(buffer, ref offset, QuantizeUnsigned(p.Leg.SegmentLength, SegmentLengthScale));

                // The paintwork, on the same terms: fixed length, meaning gated by the flag. The alpha
                // is not sent — it is the flag, and a part is either painted or it is not.
                Color32 tint = p.Tint;
                buffer[offset++] = tint.r;
                buffer[offset++] = tint.g;
                buffer[offset++] = tint.b;
                buffer[offset++] = p.PatternId;

                // The fitted part, by the same hash every other part id travels as. Zero is "nothing
                // fitted", so it needs no flag of its own.
                WriteInt32(buffer, ref offset, p.FittingId);
            }

            return buffer;
        }

        public static bool TryDecode(byte[] blob, out CreatureGenome genome, out GenomeError error)
        {
            genome = null;

            if (blob == null || blob.Length < HeaderBytesV1)
            {
                error = GenomeError.MalformedPayload;
                return false;
            }

            if (blob.Length > GenomeLimits.MaxBlobBytes)
            {
                error = GenomeError.PayloadTooLarge;
                return false;
            }

            if (blob[0] != Magic)
            {
                error = GenomeError.MalformedPayload;
                return false;
            }

            byte version = blob[1];
            if (version < MinSupportedVersion || version > Version)
            {
                error = GenomeError.UnsupportedVersion;
                return false;
            }

            // Dispatch on the version: both branches read the same layout and differ only in the
            // length of the part record and whether it carries a reshaped leg.
            return version switch
            {
                1 => TryDecodeParts(blob, HeaderBytesV1, PartBytesV1, withLeg: false, withSkin: false, withFitting: false, out genome, out error),
                2 => TryDecodeParts(blob, HeaderBytesV1, PartBytesV2, withLeg: true, withSkin: false, withFitting: false, out genome, out error),
                3 => TryDecodeParts(blob, HeaderBytes, PartBytesV3, withLeg: true, withSkin: true, withFitting: false, out genome, out error),
                4 => TryDecodeParts(blob, HeaderBytes, PartBytes, withLeg: true, withSkin: true, withFitting: true, out genome, out error),
                _ => Fail(GenomeError.UnsupportedVersion, out error),
            };
        }

        private static bool TryDecodeParts(byte[] blob, int headerBytes, int partBytes, bool withLeg, bool withSkin,
            bool withFitting, out CreatureGenome genome, out GenomeError error)
        {
            genome = null;

            int vertebraCount = blob[2];
            int partCount = blob[3];

            if (vertebraCount > GenomeLimits.MaxVertebrae) return Fail(GenomeError.TooManyVertebrae, out error);
            if (partCount > GenomeLimits.MaxParts) return Fail(GenomeError.TooManyParts, out error);

            int expected = headerBytes + vertebraCount * VertebraBytes + partCount * partBytes;
            if (blob.Length != expected) return Fail(GenomeError.MalformedPayload, out error);

            int offset = 4;
            var primary = new Color32(blob[offset++], blob[offset++], blob[offset++], 255);
            var secondary = new Color32(blob[offset++], blob[offset++], blob[offset++], 255);
            byte bodyPattern = withSkin ? blob[offset++] : (byte)0;

            var result = new CreatureGenome { PrimaryColor = primary, SecondaryColor = secondary, BodyPattern = bodyPattern };

            for (int i = 0; i < vertebraCount; i++)
            {
                Vector3 localOffset = ReadVector(blob, ref offset, PositionScale);
                Quaternion localRotation = ReadEuler(blob, ref offset);
                float radius = ReadUInt16(blob, ref offset) / RadiusScale;
                result.AddVertebra(new VertebraGene(localOffset, localRotation, radius));
            }

            for (int i = 0; i < partCount; i++)
            {
                int partId = ReadInt32(blob, ref offset);
                byte boneIndex = blob[offset++];
                Vector3 localPosition = ReadVector(blob, ref offset, PositionScale);
                Quaternion localRotation = ReadEuler(blob, ref offset);
                float scale = ReadUInt16(blob, ref offset) / PartScaleScale;
                byte flags = blob[offset++];
                bool mirrored = (flags & FlagMirrored) != 0;

                if (!withLeg)
                {
                    result.AddPart(new PartGene(partId, boneIndex, localPosition, localRotation, scale, mirrored));
                    continue;
                }

                int bendPoints = blob[offset++];
                float segmentLength = ReadUInt16(blob, ref offset) / SegmentLengthScale;

                // The LegSpec constructor clamps both numbers to the allowed range, so a doctored blob
                // cannot build a leg with two hundred links.
                bool hasLeg = (flags & FlagLegOverride) != 0;
                LegSpec leg = hasLeg ? new LegSpec(bendPoints, segmentLength) : default;

                if (!withSkin)
                {
                    result.AddPart(new PartGene(partId, boneIndex, localPosition, localRotation, scale, mirrored, hasLeg, leg));
                    continue;
                }

                // Alpha carries the flag, not the blob: a part that was never painted has to come out
                // unpainted rather than opaque black.
                byte r = blob[offset++], g = blob[offset++], b = blob[offset++];
                byte patternId = blob[offset++];
                Color32 tint = (flags & FlagTinted) != 0 ? new Color32(r, g, b, 255) : default;

                // A genome written before limbs had sockets comes out with nothing fitted — which is
                // the truth about it: its feet were part of the limb's own model.
                int fittingId = withFitting ? ReadInt32(blob, ref offset) : 0;

                result.AddPart(new PartGene(partId, boneIndex, localPosition, localRotation, scale, mirrored,
                    hasLeg, leg, tint, patternId, fittingId));
            }

            genome = result;
            error = GenomeError.None;
            return true;
        }

        /// <summary>
        /// The migration seam between format versions. It is a no-op at version 1, but it has existed
        /// from the start so that adding version 2 did not require rebuilding the decode path.
        /// </summary>
        public static CreatureGenome Upgrade(CreatureGenome genome, int fromVersion) => genome;

        private static bool Fail(GenomeError reason, out GenomeError error)
        {
            error = reason;
            return false;
        }

        // --- Read/write primitives (explicitly little-endian) ---

        private static void WriteInt16(byte[] buffer, ref int offset, short value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
        }

        private static short ReadInt16(byte[] buffer, ref int offset)
        {
            int value = buffer[offset++] | (buffer[offset++] << 8);
            return (short)value;
        }

        private static void WriteUInt16(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
        }

        private static ushort ReadUInt16(byte[] buffer, ref int offset)
        {
            int value = buffer[offset++] | (buffer[offset++] << 8);
            return (ushort)value;
        }

        private static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
            buffer[offset++] = (byte)((value >> 16) & 0xFF);
            buffer[offset++] = (byte)((value >> 24) & 0xFF);
        }

        private static int ReadInt32(byte[] buffer, ref int offset)
        {
            return buffer[offset++]
                 | (buffer[offset++] << 8)
                 | (buffer[offset++] << 16)
                 | (buffer[offset++] << 24);
        }

        private static void WriteVector(byte[] buffer, ref int offset, Vector3 value, float scale)
        {
            WriteInt16(buffer, ref offset, QuantizeSigned(value.x, scale));
            WriteInt16(buffer, ref offset, QuantizeSigned(value.y, scale));
            WriteInt16(buffer, ref offset, QuantizeSigned(value.z, scale));
        }

        private static Vector3 ReadVector(byte[] buffer, ref int offset, float scale)
        {
            float x = ReadInt16(buffer, ref offset) / scale;
            float y = ReadInt16(buffer, ref offset) / scale;
            float z = ReadInt16(buffer, ref offset) / scale;
            return new Vector3(x, y, z);
        }

        private static void WriteEuler(byte[] buffer, ref int offset, Quaternion rotation)
        {
            Vector3 euler = rotation.eulerAngles;
            WriteInt16(buffer, ref offset, QuantizeSigned(WrapAngle(euler.x), AngleScale));
            WriteInt16(buffer, ref offset, QuantizeSigned(WrapAngle(euler.y), AngleScale));
            WriteInt16(buffer, ref offset, QuantizeSigned(WrapAngle(euler.z), AngleScale));
        }

        private static Quaternion ReadEuler(byte[] buffer, ref int offset)
        {
            float x = ReadInt16(buffer, ref offset) / AngleScale;
            float y = ReadInt16(buffer, ref offset) / AngleScale;
            float z = ReadInt16(buffer, ref offset) / AngleScale;
            return Quaternion.Euler(x, y, z);
        }

        /// <summary>Brings an angle from [0, 360) into [-180, 180] so it fits an int16 at a scale of 1/100.</summary>
        private static float WrapAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees < -180f) degrees += 360f;
            return degrees;
        }

        /// <summary>
        /// Quantisation to int16. The range follows from the scale itself: at 1/1000 m it holds
        /// ±32.767 m of position, at 1/100 degree ±327.67° of angle — in both cases with room to spare
        /// over the genome limits.
        /// </summary>
        private static short QuantizeSigned(float value, float scale)
        {
            float scaled = Mathf.Round(value * scale);
            return (short)Mathf.Clamp(scaled, short.MinValue, short.MaxValue);
        }

        private static ushort QuantizeUnsigned(float value, float scale)
        {
            float scaled = Mathf.Max(0f, value) * scale;
            return (ushort)Mathf.Clamp(Mathf.Round(scaled), 0, ushort.MaxValue);
        }
    }
}
