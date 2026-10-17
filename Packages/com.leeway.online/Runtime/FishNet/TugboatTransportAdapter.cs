using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Leeway.Online.FishNet
{
    /// <summary>Plain UDP via Tugboat: LAN play, direct IP, dedicated servers.</summary>
    [CreateAssetMenu(menuName = "Leeway/Online/FishNet/Tugboat Transport Adapter", fileName = "TugboatTransportAdapter")]
    public sealed class TugboatTransportAdapter : TransportAdapter<Tugboat>
    {
        [Tooltip("Advertise the host's LAN address in the lobby. Off: hosts only advertise the override below (if any).")]
        [SerializeField] private bool _advertiseLanAddress = true;

        [Tooltip("An address to advertise instead of the detected LAN one — e.g. a public IP with a forwarded port.")]
        [SerializeField] private string _advertisedAddressOverride = string.Empty;

        public override string Scheme => NetworkEndpoint.UdpScheme;

        public override bool TryGetHostEndpoint(NetworkManager networkManager, out NetworkEndpoint endpoint)
        {
            endpoint = default;
            if (!TryFind(networkManager, out var tugboat)) return false;

            string address = !string.IsNullOrWhiteSpace(_advertisedAddressOverride)
                ? _advertisedAddressOverride.Trim()
                : _advertiseLanAddress ? FindLanAddress() : null;
            if (string.IsNullOrEmpty(address)) return false;

            endpoint = new NetworkEndpoint(Scheme, address, tugboat.GetPort());
            return true;
        }

        public override OnlineResult ConfigureClient(NetworkManager networkManager, NetworkEndpoint endpoint)
        {
            if (!TryFind(networkManager, out var tugboat))
                return OnlineResult.Failure(OnlineErrorCode.NotSupported, "The NetworkManager has no Tugboat transport.");

            tugboat.SetClientAddress(endpoint.Address);
            if (endpoint.Port != 0) tugboat.SetPort(endpoint.Port);
            SelectForClient(networkManager, tugboat);
            return OnlineResult.Success;
        }

        /// <summary>The first operational, non-loopback IPv4 address — what another machine on the LAN would dial.</summary>
        public static string FindLanAddress()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        var ip = unicast.Address;
                        if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                            return ip.ToString();
                    }
                }
            }
            catch (NetworkInformationException)
            {
                // Some platforms do not expose interfaces; fall through to loopback.
            }
            return IPAddress.Loopback.ToString();
        }
    }
}
