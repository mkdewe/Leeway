using System;

namespace Leeway.Online
{
    /// <summary>
    /// A lobby on some backend, in the backend's canonical string form.
    /// </summary>
    /// <remarks>
    /// The string form doubles as the "lobby code" a player can paste to join, so backends should keep
    /// it short and copy-friendly where they have the choice.
    /// </remarks>
    [Serializable]
    public readonly struct LobbyId : IEquatable<LobbyId>
    {
        public static readonly LobbyId None = default;

        public readonly string Value;

        public LobbyId(string value) => Value = value?.Trim() ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(LobbyId other) => string.Equals(Value ?? string.Empty, other.Value ?? string.Empty, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LobbyId other && Equals(other);
        public override int GetHashCode() => (Value ?? string.Empty).GetHashCode();
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(LobbyId a, LobbyId b) => a.Equals(b);
        public static bool operator !=(LobbyId a, LobbyId b) => !a.Equals(b);
    }
}
