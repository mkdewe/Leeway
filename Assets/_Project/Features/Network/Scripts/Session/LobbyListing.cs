using Leeway.Online;

namespace Leeway.Network.Session
{
    /// <summary>A lobby browser row, decoded from a <see cref="LobbySummary"/> into the game's terms.</summary>
    public readonly struct LobbyListing
    {
        public readonly LobbyId Id;
        public readonly string Name;
        public readonly GameMode Mode;
        public readonly LobbyPhase Phase;
        public readonly int Players;
        public readonly int MaxPlayers;

        public LobbyListing(LobbySummary summary)
        {
            Id = summary.Id;
            Name = summary.Name;
            Mode = LobbySnapshot.ParseEnum(summary.GetData(GameLobbyKeys.Mode), GameMode.Coop);
            Phase = LobbySnapshot.ParseEnum(summary.GetData(GameLobbyKeys.Phase), LobbyPhase.Gathering);
            Players = summary.MemberCount;
            MaxPlayers = summary.MaxMembers;
        }

        public bool IsFull => Players >= MaxPlayers;
    }

    /// <summary>What the player filters the browser by. <c>null</c> mode means any.</summary>
    public readonly struct LobbyBrowserFilter
    {
        public readonly GameMode? Mode;
        public readonly bool OnlyWithFreeSlots;
        public readonly string NameContains;

        public LobbyBrowserFilter(GameMode? mode, bool onlyWithFreeSlots, string nameContains)
        {
            Mode = mode;
            OnlyWithFreeSlots = onlyWithFreeSlots;
            NameContains = nameContains;
        }
    }
}
