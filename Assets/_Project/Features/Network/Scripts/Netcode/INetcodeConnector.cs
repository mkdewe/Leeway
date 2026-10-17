using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Leeway.Online;

namespace Leeway.Network.Netcode
{
    /// <summary>
    /// Starts and stops the game's network connection. The session talks to this, never to FishNet
    /// directly, so the connect flow can be tested and a different netcode slotted in.
    /// </summary>
    public interface INetcodeConnector
    {
        bool IsServerRunning { get; }
        bool IsClientRunning { get; }

        /// <summary>Starts server + local client. Completes when both run.</summary>
        UniTask<OnlineResult> StartHostAsync(int maxPlayers, CancellationToken cancellationToken);

        /// <summary>Dials the first of <paramref name="endpoints"/> this build can reach. Completes when connected.</summary>
        UniTask<OnlineResult> ConnectAsync(IReadOnlyList<NetworkEndpoint> endpoints, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>The endpoints a running host should advertise in its lobby.</summary>
        IReadOnlyList<NetworkEndpoint> GetHostEndpoints();

        /// <summary>Host only: loads the match scene for everyone, replacing the menu.</summary>
        void LoadSceneForAll(string sceneName);

        void Stop();

        /// <summary>The client connection dropped without <see cref="Stop"/> being called (host quit, timeout, kick).</summary>
        event Action ConnectionLost;
    }
}
