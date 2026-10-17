using System;

namespace Leeway.Online
{
    /// <summary>
    /// What a backend can do. The UI asks this instead of asking "is this Steam?", so a new backend
    /// lights up exactly the screens it supports without anyone touching the menu code.
    /// </summary>
    [Flags]
    public enum OnlineCapabilities
    {
        None = 0,

        /// <summary>Creating and joining lobbies.</summary>
        Lobbies = 1 << 0,

        /// <summary>Lobbies are visible to other machines — false for the in-process Local backend.</summary>
        RemoteLobbies = 1 << 1,

        /// <summary>Browsing public lobbies with filters.</summary>
        LobbySearch = 1 << 2,

        /// <summary>Text chat carried by the lobby itself.</summary>
        LobbyChat = 1 << 3,

        /// <summary>Per-member key/value data (ready flags, loadouts).</summary>
        MemberData = 1 << 4,

        /// <summary>The platform's own "invite friends" UI.</summary>
        InviteDialog = 1 << 5,

        /// <summary>Joining by pasting a lobby id/code.</summary>
        JoinByCode = 1 << 6,

        /// <summary>The platform relays game traffic (no port forwarding needed).</summary>
        RelayTransport = 1 << 7,
    }

    public static class OnlineCapabilitiesExtensions
    {
        public static bool Has(this OnlineCapabilities self, OnlineCapabilities flags) => (self & flags) == flags;
    }
}
