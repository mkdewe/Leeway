using System;
using System.Globalization;

namespace Leeway.Online
{
    /// <summary>
    /// "Where the host's game server listens", in a netcode-neutral form the lobby can carry.
    /// </summary>
    /// <remarks>
    /// <para>The <see cref="Scheme"/> names the kind of transport ("udp" for a plain IP socket,
    /// "steam" for Steam's relay, later "eos", "ugs-relay" …); the address is whatever that transport
    /// dials — an IP, a SteamID, a relay join code. The online layer never interprets it; a netcode
    /// bridge maps schemes to transports.</para>
    ///
    /// <para>Wire form: <c>scheme|address|port</c> — a pipe, because IPv6 addresses and relay codes
    /// both contain colons.</para>
    /// </remarks>
    public readonly struct NetworkEndpoint : IEquatable<NetworkEndpoint>
    {
        public const string UdpScheme = "udp";
        public const string SteamScheme = "steam";

        private const char Separator = '|';

        public readonly string Scheme;
        public readonly string Address;
        public readonly ushort Port;

        public NetworkEndpoint(string scheme, string address, ushort port = 0)
        {
            Scheme = (scheme ?? string.Empty).Trim().ToLowerInvariant();
            Address = (address ?? string.Empty).Trim();
            Port = port;
        }

        public bool IsValid => Scheme.Length > 0 && Address.Length > 0 && Scheme.IndexOf(Separator) < 0 && Address.IndexOf(Separator) < 0;

        public string Serialize() => string.Concat(Scheme, Separator.ToString(), Address, Separator.ToString(), Port.ToString(CultureInfo.InvariantCulture));

        public static bool TryParse(string text, out NetworkEndpoint endpoint)
        {
            endpoint = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split(Separator);
            if (parts.Length != 3) return false;
            if (!ushort.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out ushort port)) return false;

            endpoint = new NetworkEndpoint(parts[0], parts[1], port);
            return endpoint.IsValid;
        }

        public bool Equals(NetworkEndpoint other) => Scheme == other.Scheme && Address == other.Address && Port == other.Port;
        public override bool Equals(object obj) => obj is NetworkEndpoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Scheme, Address, Port);
        public override string ToString() => Port == 0 ? $"{Scheme}://{Address}" : $"{Scheme}://{Address}:{Port}";
    }
}
