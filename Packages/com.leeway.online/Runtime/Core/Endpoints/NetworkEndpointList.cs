using System.Collections.Generic;
using System.Text;

namespace Leeway.Online
{
    /// <summary>
    /// The host's endpoints in order of preference, stored in one lobby data value
    /// (<see cref="LobbyDataKeys.Endpoints"/>).
    /// </summary>
    /// <remarks>
    /// A host may be reachable several ways at once — Steam relay for friends across the world and a
    /// LAN IP for the person on the sofa. Each client picks the first scheme its own netcode bridge
    /// supports.
    /// </remarks>
    public static class NetworkEndpointList
    {
        private const char Separator = ';';

        public static string Serialize(IEnumerable<NetworkEndpoint> endpoints)
        {
            var sb = new StringBuilder();
            foreach (var endpoint in endpoints)
            {
                if (!endpoint.IsValid) continue;
                if (sb.Length > 0) sb.Append(Separator);
                sb.Append(endpoint.Serialize());
            }
            return sb.ToString();
        }

        /// <summary>Parses what <see cref="Serialize"/> wrote, silently skipping malformed entries.</summary>
        public static List<NetworkEndpoint> Parse(string text)
        {
            var result = new List<NetworkEndpoint>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (var part in text.Split(Separator))
                if (NetworkEndpoint.TryParse(part, out var endpoint)) result.Add(endpoint);
            return result;
        }

        public static OnlineResult Publish(this ILobby lobby, IEnumerable<NetworkEndpoint> endpoints) =>
            lobby.SetData(LobbyDataKeys.Endpoints, Serialize(endpoints));

        public static List<NetworkEndpoint> ReadEndpoints(this ILobby lobby) => Parse(lobby.GetData(LobbyDataKeys.Endpoints));
    }
}
