using System;
using FishNet.Managing;
using Steamworks;
using UnityEngine;

namespace Leeway.Online.FishNet.Steam
{
    /// <summary>Steam Networking Sockets via FishySteamworks: NAT punch-through and relay, addressed by SteamID.</summary>
    /// <remarks>
    /// The transport must have <c>Peer To Peer</c> enabled on the prefab — in dedicated-server mode
    /// FishySteamworks expects a Steam game server, which this game does not run.
    /// </remarks>
    [CreateAssetMenu(menuName = "Leeway/Online/FishNet/FishySteamworks Transport Adapter", fileName = "FishySteamworksTransportAdapter")]
    public sealed class FishySteamworksTransportAdapter : TransportAdapter<global::FishySteamworks.FishySteamworks>
    {
        public override string Scheme => NetworkEndpoint.SteamScheme;

        public override bool TryGetHostEndpoint(NetworkManager networkManager, out NetworkEndpoint endpoint)
        {
            endpoint = default;
            if (!TryFind(networkManager, out _) || !TryGetLocalSteamId(out var steamId)) return false;

            endpoint = new NetworkEndpoint(Scheme, steamId.m_SteamID.ToString());
            return true;
        }

        public override OnlineResult ConfigureClient(NetworkManager networkManager, NetworkEndpoint endpoint)
        {
            if (!TryFind(networkManager, out var transport))
                return OnlineResult.Failure(OnlineErrorCode.NotSupported, "The NetworkManager has no FishySteamworks transport.");
            if (!TryGetLocalSteamId(out _))
                return OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, "Steam is not running — cannot dial a Steam endpoint.");
            if (!ulong.TryParse(endpoint.Address, out _))
                return OnlineResult.Failure(OnlineErrorCode.InvalidArgument, $"'{endpoint.Address}' is not a SteamID.");

            transport.SetClientAddress(endpoint.Address);
            SelectForClient(networkManager, transport);
            return OnlineResult.Success;
        }

        /// <summary>The Steam API throws when it is not initialized; that just means "no Steam here".</summary>
        private static bool TryGetLocalSteamId(out CSteamID steamId)
        {
            try
            {
                steamId = SteamUser.GetSteamID();
                return steamId.IsValid();
            }
            catch (InvalidOperationException)
            {
                steamId = CSteamID.Nil;
                return false;
            }
        }
    }
}
