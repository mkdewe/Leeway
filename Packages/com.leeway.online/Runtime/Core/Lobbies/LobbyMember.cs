using System;

namespace Leeway.Online
{
    /// <summary>A member of the current lobby, as known when the event fired.</summary>
    public readonly struct LobbyMember : IEquatable<LobbyMember>
    {
        public readonly UserId Id;
        public readonly string DisplayName;

        public LobbyMember(UserId id, string displayName)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
        }

        public bool Equals(LobbyMember other) => Id.Equals(other.Id);
        public override bool Equals(object obj) => obj is LobbyMember other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => DisplayName;
    }
}
