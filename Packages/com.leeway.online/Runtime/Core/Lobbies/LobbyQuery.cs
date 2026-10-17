using System;
using System.Collections.Generic;

namespace Leeway.Online
{
    public enum LobbyFilterComparison
    {
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    /// <summary>How far away lobbies may be. Backends without geography ignore it.</summary>
    public enum LobbySearchDistance
    {
        Close,
        Default,
        Far,
        Worldwide,
    }

    /// <summary>One condition on lobby data. Either <see cref="Text"/> or <see cref="Number"/> is set.</summary>
    public readonly struct LobbyFilter
    {
        public readonly string Key;
        public readonly LobbyFilterComparison Comparison;
        public readonly string Text;
        public readonly int? Number;

        private LobbyFilter(string key, LobbyFilterComparison comparison, string text, int? number)
        {
            Key = key;
            Comparison = comparison;
            Text = text;
            Number = number;
        }

        public bool IsNumeric => Number.HasValue;

        public static LobbyFilter ForText(string key, LobbyFilterComparison comparison, string value) => new(key, comparison, value ?? string.Empty, null);
        public static LobbyFilter ForNumber(string key, LobbyFilterComparison comparison, int value) => new(key, comparison, null, value);

        /// <summary>Evaluates the filter against plain data — for backends that filter on the client.</summary>
        public bool Matches(string actual)
        {
            if (IsNumeric)
            {
                if (!int.TryParse(actual, out int n)) return false;
                int v = Number.Value;
                return Comparison switch
                {
                    LobbyFilterComparison.Equal => n == v,
                    LobbyFilterComparison.NotEqual => n != v,
                    LobbyFilterComparison.LessThan => n < v,
                    LobbyFilterComparison.LessThanOrEqual => n <= v,
                    LobbyFilterComparison.GreaterThan => n > v,
                    LobbyFilterComparison.GreaterThanOrEqual => n >= v,
                    _ => false,
                };
            }

            int c = string.CompareOrdinal(actual ?? string.Empty, Text);
            return Comparison switch
            {
                LobbyFilterComparison.Equal => c == 0,
                LobbyFilterComparison.NotEqual => c != 0,
                LobbyFilterComparison.LessThan => c < 0,
                LobbyFilterComparison.LessThanOrEqual => c <= 0,
                LobbyFilterComparison.GreaterThan => c > 0,
                LobbyFilterComparison.GreaterThanOrEqual => c >= 0,
                _ => false,
            };
        }
    }

    /// <summary>
    /// An immutable lobby search. Build it with <see cref="Create"/>:
    /// <code>LobbyQuery.Create().WhereEquals("mode", "coop").WithFreeSlots(1).Limit(50).Build()</code>
    /// </summary>
    public sealed class LobbyQuery
    {
        public const int DefaultMaxResults = 50;

        public IReadOnlyList<LobbyFilter> Filters { get; }
        public int MinFreeSlots { get; }
        public int MaxResults { get; }
        public LobbySearchDistance Distance { get; }

        private LobbyQuery(IReadOnlyList<LobbyFilter> filters, int minFreeSlots, int maxResults, LobbySearchDistance distance)
        {
            Filters = filters;
            MinFreeSlots = minFreeSlots;
            MaxResults = maxResults;
            Distance = distance;
        }

        public static Builder Create() => new();

        /// <summary>Whether a lobby passes every filter — for backends that filter on the client.</summary>
        public bool Matches(LobbySummary lobby)
        {
            if (lobby.MaxMembers - lobby.MemberCount < MinFreeSlots) return false;
            foreach (var filter in Filters)
                if (!filter.Matches(lobby.GetData(filter.Key))) return false;
            return true;
        }

        public sealed class Builder
        {
            private readonly List<LobbyFilter> _filters = new();
            private int _minFreeSlots;
            private int _maxResults = DefaultMaxResults;
            private LobbySearchDistance _distance = LobbySearchDistance.Default;

            internal Builder() { }

            public Builder WhereEquals(string key, string value) => Where(LobbyFilter.ForText(key, LobbyFilterComparison.Equal, value));
            public Builder WhereNotEquals(string key, string value) => Where(LobbyFilter.ForText(key, LobbyFilterComparison.NotEqual, value));
            public Builder WhereNumber(string key, LobbyFilterComparison comparison, int value) => Where(LobbyFilter.ForNumber(key, comparison, value));

            public Builder Where(LobbyFilter filter)
            {
                if (string.IsNullOrEmpty(filter.Key)) throw new ArgumentException("A lobby filter needs a key.", nameof(filter));
                _filters.Add(filter);
                return this;
            }

            /// <summary>Only lobbies of this protocol version — see <see cref="LobbyDataKeys.ProtocolVersion"/>.</summary>
            public Builder WithProtocolVersion(string version) => Where(LobbyFilter.ForText(LobbyDataKeys.ProtocolVersion, LobbyFilterComparison.Equal, version));

            public Builder WithFreeSlots(int slots)
            {
                _minFreeSlots = Math.Max(0, slots);
                return this;
            }

            public Builder Limit(int maxResults)
            {
                _maxResults = Math.Max(1, maxResults);
                return this;
            }

            public Builder Within(LobbySearchDistance distance)
            {
                _distance = distance;
                return this;
            }

            public LobbyQuery Build() => new(_filters.ToArray(), _minFreeSlots, _maxResults, _distance);
        }
    }
}
