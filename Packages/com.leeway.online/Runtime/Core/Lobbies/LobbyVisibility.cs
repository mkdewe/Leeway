namespace Leeway.Online
{
    /// <summary>Who can find and enter a lobby. Backends map these onto their nearest equivalent.</summary>
    public enum LobbyVisibility
    {
        /// <summary>Listed in search; anyone can join.</summary>
        Public,

        /// <summary>Not listed; friends of members can see and join it through the platform.</summary>
        FriendsOnly,

        /// <summary>Invite or lobby code only.</summary>
        Private,
    }
}
