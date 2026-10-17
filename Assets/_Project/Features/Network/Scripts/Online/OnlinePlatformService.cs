using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Leeway.Network.Messages;
using Leeway.Network.Session;
using Leeway.Online;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Leeway.Network.Online
{
    /// <summary>
    /// Brings up the first available online platform at startup and owns it until the game quits.
    /// </summary>
    public sealed class OnlinePlatformService : IOnlinePlatformAccessor, IAsyncStartable, IDisposable
    {
        private readonly NetworkSessionSettings _settings;
        private readonly UniTaskCompletionSource<IOnlinePlatform> _ready = new();
        private readonly CancellationTokenSource _lifetime = new();

        public OnlinePlatformService(NetworkSessionSettings settings) => _settings = settings;

        public IOnlinePlatform Platform { get; private set; }
        public bool IsReady => Platform != null;

        public event Action<IOnlinePlatform> Ready;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);

            var outcome = await OnlinePlatformBootstrapper.StartFirstAvailableAsync(_settings.GetPlatformProviders(), linked.Token);

            foreach (var failure in outcome.FailedAttempts)
                Debug.LogWarning($"[Online] {failure.Platform} unavailable — {failure.Error}");

            Platform = outcome.Platform;
            Platform.Disconnected += OnPlatformDisconnected;
            Debug.Log($"[Online] Running on {Platform.DisplayName} as {Platform.LocalUser}.");

            GlobalMessagePipe.GetPublisher<OnlinePlatformReadyMessage>().Publish(new OnlinePlatformReadyMessage(
                Platform.Id, Platform.DisplayName, Platform.LocalUser, Platform.Capabilities));

            _ready.TrySetResult(Platform);
            Ready?.Invoke(Platform);
        }

        public UniTask<IOnlinePlatform> WhenReadyAsync(CancellationToken cancellationToken = default) =>
            _ready.Task.AttachExternalCancellation(cancellationToken);

        private void OnPlatformDisconnected(OnlineError error) =>
            GlobalMessagePipe.GetPublisher<SessionErrorMessage>().Publish(new SessionErrorMessage(
                "Connection to the platform lost",
                $"{Platform.DisplayName} reported a disconnect. Online features may not work until it reconnects.",
                error));

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _ready.TrySetCanceled();

            if (Platform == null) return;
            Platform.Disconnected -= OnPlatformDisconnected;
            Platform.Dispose();
            Platform = null;
        }
    }
}
