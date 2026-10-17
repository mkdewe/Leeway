using System.Collections.Generic;

namespace Leeway.Online
{
    /// <summary>A search result: enough to render a row in a lobby browser, not a live lobby.</summary>
    public sealed class LobbySummary
    {
        private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

        public LobbyId Id { get; }
        public int MemberCount { get; }
        public int MaxMembers { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public LobbySummary(LobbyId id, int memberCount, int maxMembers, IReadOnlyDictionary<string, string> data)
        {
            Id = id;
            MemberCount = memberCount;
            MaxMembers = maxMembers;
            Data = data ?? Empty;
        }

        public string Name => GetData(LobbyDataKeys.Name) is { Length: > 0 } name ? name : Id.ToString();
        public bool IsFull => MemberCount >= MaxMembers;

        public string GetData(string key) => key != null && Data.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
