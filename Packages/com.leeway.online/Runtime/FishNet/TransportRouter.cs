using System.Collections.Generic;
using FishNet.Managing;

namespace Leeway.Online.FishNet
{
    /// <summary>
    /// Matches endpoints to transports: what a host advertises, and which advertised endpoint a
    /// client should dial with the transports it has.
    /// </summary>
    public sealed class TransportRouter
    {
        private readonly List<TransportAdapter> _adapters = new();

        /// <param name="adapters">In order of preference — the host advertises in this order, clients dial the first match.</param>
        public TransportRouter(IEnumerable<TransportAdapter> adapters)
        {
            if (adapters == null) return;
            foreach (var adapter in adapters)
                if (adapter != null) _adapters.Add(adapter);
        }

        public IReadOnlyList<TransportAdapter> Adapters => _adapters;

        public List<NetworkEndpoint> GetHostEndpoints(NetworkManager networkManager)
        {
            var endpoints = new List<NetworkEndpoint>();
            foreach (var adapter in _adapters)
                if (adapter.TryGetHostEndpoint(networkManager, out var endpoint)) endpoints.Add(endpoint);
            return endpoints;
        }

        /// <summary>Configures the client for the first advertised endpoint it can reach.</summary>
        public OnlineResult<NetworkEndpoint> ConfigureClient(NetworkManager networkManager, IReadOnlyList<NetworkEndpoint> advertised)
        {
            if (advertised == null || advertised.Count == 0)
                return OnlineResult<NetworkEndpoint>.Failure(OnlineErrorCode.ConnectionFailed, "The host did not advertise any endpoint.");

            OnlineError lastError = default;
            foreach (var endpoint in advertised)
            {
                var adapter = Find(endpoint.Scheme);
                if (adapter == null || !adapter.IsPresent(networkManager)) continue;

                var result = adapter.ConfigureClient(networkManager, endpoint);
                if (result.IsSuccess) return OnlineResult<NetworkEndpoint>.Success(endpoint);
                lastError = result.Error;
            }

            return lastError.IsError
                ? OnlineResult<NetworkEndpoint>.Failure(lastError)
                : OnlineResult<NetworkEndpoint>.Failure(OnlineErrorCode.ConnectionFailed, "None of the host's endpoints can be reached with this build's transports.");
        }

        public TransportAdapter Find(string scheme)
        {
            foreach (var adapter in _adapters)
                if (adapter.Scheme == scheme) return adapter;
            return null;
        }
    }
}
