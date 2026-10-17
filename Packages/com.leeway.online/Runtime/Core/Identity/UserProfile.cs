namespace Leeway.Online
{
    /// <summary>What the game is allowed to know about a platform user: who they are and what to call them.</summary>
    public readonly struct UserProfile
    {
        public static readonly UserProfile Anonymous = new(UserId.None, "Player");

        public readonly UserId Id;
        public readonly string DisplayName;

        public UserProfile(UserId id, string displayName)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName;
        }

        public override string ToString() => $"{DisplayName} ({Id})";
    }
}
