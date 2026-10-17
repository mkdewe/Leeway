using System;

namespace Leeway.Online
{
    /// <summary>
    /// Names an online backend ("steam", "eos", "psn" …).
    /// </summary>
    /// <remarks>
    /// A string rather than an enum on purpose: a new backend lives in its own assembly and must be
    /// able to name itself without editing this one. The well-known ids below are only conventions.
    /// </remarks>
    [Serializable]
    public readonly struct PlatformId : IEquatable<PlatformId>
    {
        public static readonly PlatformId Offline = new("offline");
        public static readonly PlatformId Local = new("local");
        public static readonly PlatformId Steam = new("steam");
        public static readonly PlatformId EpicOnlineServices = new("eos");
        public static readonly PlatformId UnityGamingServices = new("ugs");
        public static readonly PlatformId PlayStation = new("psn");
        public static readonly PlatformId Xbox = new("xbl");

        public readonly string Value;

        public PlatformId(string value) => Value = value ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(PlatformId other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is PlatformId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        public override string ToString() => Value;

        public static bool operator ==(PlatformId a, PlatformId b) => a.Equals(b);
        public static bool operator !=(PlatformId a, PlatformId b) => !a.Equals(b);
    }
}
