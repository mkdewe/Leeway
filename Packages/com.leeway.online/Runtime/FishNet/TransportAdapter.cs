using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using UnityEngine;

namespace Leeway.Online.FishNet
{
    /// <summary>
    /// Teaches the FishNet bridge one endpoint scheme: which transport serves it, what the host
    /// advertises, and how a client dials it.
    /// </summary>
    /// <remarks>
    /// <para>A strategy per scheme, authored as an asset for the same reason platform providers are:
    /// the Steam adapter lives in an assembly that does not exist on consoles, and a missing asset
    /// reference is the cleanest way to say "not here".</para>
    ///
    /// <para>Adapters find their transport on the <see cref="NetworkManager"/> either as the active
    /// transport or as one of <see cref="Multipass"/>'s — so one NetworkManager can serve Steam relay
    /// and LAN at the same time.</para>
    /// </remarks>
    public abstract class TransportAdapter : ScriptableObject
    {
        public abstract string Scheme { get; }

        /// <summary>Whether the NetworkManager carries a transport this adapter can drive.</summary>
        public abstract bool IsPresent(NetworkManager networkManager);

        /// <summary>
        /// The endpoint a running host advertises for this scheme. False when the scheme cannot be
        /// offered right now (transport missing, platform not signed in).
        /// </summary>
        public abstract bool TryGetHostEndpoint(NetworkManager networkManager, out NetworkEndpoint endpoint);

        /// <summary>Points the client side at <paramref name="endpoint"/> and makes this transport the one the client uses.</summary>
        public abstract OnlineResult ConfigureClient(NetworkManager networkManager, NetworkEndpoint endpoint);
    }

    /// <summary>The common half of every adapter: locating <typeparamref name="T"/>, directly or inside Multipass.</summary>
    public abstract class TransportAdapter<T> : TransportAdapter where T : Transport
    {
        public override bool IsPresent(NetworkManager networkManager) => TryFind(networkManager, out _);

        protected static bool TryFind(NetworkManager networkManager, out T transport)
        {
            transport = null;
            if (networkManager == null) return false;

            var active = networkManager.TransportManager.Transport;
            if (active is T direct)
            {
                transport = direct;
                return true;
            }

            if (active is Multipass multipass)
            {
                transport = multipass.GetTransport<T>();
                return transport != null;
            }

            return false;
        }

        /// <summary>With Multipass, the client can only use one transport — select ours.</summary>
        protected static void SelectForClient(NetworkManager networkManager, T transport)
        {
            if (networkManager.TransportManager.Transport is Multipass multipass)
                multipass.SetClientTransport(transport);
        }
    }
}
