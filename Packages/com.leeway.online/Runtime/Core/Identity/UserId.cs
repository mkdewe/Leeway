using System;

namespace Leeway.Online
{
    /// <summary>
    /// A platform user, opaque to everything above the backend.
    /// </summary>
    /// <remarks>
    /// Every platform has its own id shape — a 64-bit SteamID, an EOS ProductUserId string, a PSN
    /// account id — so the abstraction keeps the backend's canonical string form and never parses it.
    /// Only the backend that produced an id may interpret it.
    /// </remarks>
    [Serializable]
    public readonly struct UserId : IEquatable<UserId>
    {
        public static readonly UserId None = default;

        public readonly string Value;

        public UserId(string value) => Value = value ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(UserId other) => string.Equals(Value ?? string.Empty, other.Value ?? string.Empty, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is UserId other && Equals(other);
        public override int GetHashCode() => (Value ?? string.Empty).GetHashCode();
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(UserId a, UserId b) => a.Equals(b);
        public static bool operator !=(UserId a, UserId b) => !a.Equals(b);
    }
}
