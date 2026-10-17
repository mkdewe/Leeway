using System;
using System.Collections.Generic;

namespace Leeway.Online
{
    /// <summary>What to create. The data is written together with the lobby, before anyone can see it.</summary>
    public sealed class LobbyCreateOptions
    {
        public string Name { get; }
        public int MaxMembers { get; }
        public LobbyVisibility Visibility { get; }
        public IReadOnlyDictionary<string, string> Data => _data;

        private readonly Dictionary<string, string> _data = new();

        public LobbyCreateOptions(string name, int maxMembers, LobbyVisibility visibility = LobbyVisibility.Public)
        {
            if (maxMembers < 1) throw new ArgumentOutOfRangeException(nameof(maxMembers), maxMembers, "A lobby needs room for at least its owner.");

            Name = string.IsNullOrWhiteSpace(name) ? "Lobby" : name.Trim();
            MaxMembers = maxMembers;
            Visibility = visibility;
            _data[LobbyDataKeys.Name] = Name;
        }

        /// <summary>Adds initial lobby data. Keys starting with <see cref="LobbyDataKeys.ReservedPrefix"/> belong to the online layer.</summary>
        public LobbyCreateOptions WithData(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Lobby data key must not be empty.", nameof(key));
            _data[key] = value ?? string.Empty;
            return this;
        }

        /// <summary>Stamps the protocol version that search will later filter on.</summary>
        public LobbyCreateOptions WithProtocolVersion(string version) => WithData(LobbyDataKeys.ProtocolVersion, version);
    }
}
