using Leeway.Network.Session;
using Leeway.Online;

namespace Leeway.MainMenu.Presenters
{
    /// <summary>Player-facing names for game enums, kept in one place for later localization.</summary>
    internal static class MenuText
    {
        public static readonly GameMode[] HostableModes = { GameMode.Coop, GameMode.Versus };
        public static readonly LobbyVisibility[] Visibilities = { LobbyVisibility.Public, LobbyVisibility.FriendsOnly, LobbyVisibility.Private };

        public static string Mode(GameMode mode) => mode switch
        {
            GameMode.Solo => "Solo",
            GameMode.Coop => "Co-op",
            GameMode.Versus => "Versus",
            _ => mode.ToString(),
        };

        public static string Visibility(LobbyVisibility visibility) => visibility switch
        {
            LobbyVisibility.Public => "Public",
            LobbyVisibility.FriendsOnly => "Friends only",
            LobbyVisibility.Private => "Invite only",
            _ => visibility.ToString(),
        };
    }
}
