namespace Leeway.Creature.Domain
{
    /// <summary>
    /// Why a genome or an edit operation was rejected. It travels over the network
    /// (server → owner) as the commit result, so the ordering of the values is part of
    /// the contract — only ever append new values at the end.
    /// </summary>
    public enum GenomeError : byte
    {
        None = 0,

        // Spine structure
        NullGenome = 1,
        TooFewVertebrae = 2,
        TooManyVertebrae = 3,
        RadiusOutOfRange = 4,
        SegmentTooLong = 5,
        VertebraIndexOutOfRange = 6,

        // Parts
        TooManyParts = 10,
        BoneIndexOutOfRange = 11,
        UnknownPart = 12,
        PartIndexOutOfRange = 13,
        InvalidAttachmentSite = 14,
        MirrorNotSupported = 15,
        ScaleOutOfRange = 16,

        // Transport
        PayloadTooLarge = 30,
        MalformedPayload = 31,
        UnsupportedVersion = 32,

        // Server
        RateLimited = 40,
        NotInEditorPad = 41,

        // Budget. Values 20-23 carried the retired per-category cardinality limits and
        // are deliberately left unused — these codes go over the wire, so numbers are never recycled.
        InsufficientFunds = 50,
    }
}
