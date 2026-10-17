using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Online.Tests
{
    public class PlatformBootstrapperTests
    {
        private sealed class FakeProvider : OnlinePlatformProvider
        {
            public string Id = "fake";
            public bool Fails;
            public bool Throws;
            public FakePlatform Created;

            public override PlatformId PlatformId => new(Id);

            public override IOnlinePlatform CreatePlatform()
            {
                if (Throws) throw new InvalidOperationException("boom");
                return Created = new FakePlatform(PlatformId, Fails);
            }
        }

        private sealed class FakePlatform : IOnlinePlatform
        {
            private readonly bool _fails;
            public bool Disposed;

            public FakePlatform(PlatformId id, bool fails)
            {
                Id = id;
                _fails = fails;
            }

            public PlatformId Id { get; }
            public string DisplayName => Id.Value;
            public OnlineCapabilities Capabilities => OnlineCapabilities.None;
            public PlatformStatus Status => PlatformStatus.Ready;
            public UserProfile LocalUser => UserProfile.Anonymous;
            public ILobbyService Lobbies => NullLobbyService.Instance;
            public ISocialService Social => NullSocialService.Instance;
            public event Action<OnlineError> Disconnected { add { } remove { } }

            public UniTask<OnlineResult> InitializeAsync(CancellationToken cancellationToken = default) =>
                UniTask.FromResult(_fails ? OnlineResult.Failure(OnlineErrorCode.PlatformUnavailable, "no client") : OnlineResult.Success);

            public void Dispose() => Disposed = true;
        }

        private static FakeProvider Provider(string id, bool fails = false, bool throws = false)
        {
            var p = ScriptableObject.CreateInstance<FakeProvider>();
            p.Id = id;
            p.Fails = fails;
            p.Throws = throws;
            return p;
        }

        [Test]
        public void FirstWorkingProviderWins_AndFailuresAreDisposedAndReported()
        {
            var steam = Provider("steam", fails: true);
            var local = Provider("local");

            var outcome = OnlinePlatformBootstrapper.StartFirstAvailableAsync(new OnlinePlatformProvider[] { steam, null, local }).GetAwaiter().GetResult();

            Assert.AreEqual("local", outcome.Platform.Id.Value);
            Assert.IsTrue(steam.Created.Disposed, "a failed candidate must not leak");
            Assert.AreEqual(1, outcome.FailedAttempts.Count);
            Assert.AreEqual(OnlineErrorCode.PlatformUnavailable, outcome.FailedAttempts[0].Error.Code);
        }

        [Test]
        public void ThrowingProvider_IsSkipped()
        {
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            var outcome = OnlinePlatformBootstrapper.StartFirstAvailableAsync(new OnlinePlatformProvider[] { Provider("bad", throws: true), Provider("ok") }).GetAwaiter().GetResult();
            Assert.AreEqual("ok", outcome.Platform.Id.Value);
        }

        [Test]
        public void NothingWorks_FallsBackToOffline()
        {
            var outcome = OnlinePlatformBootstrapper.StartFirstAvailableAsync(new OnlinePlatformProvider[] { Provider("steam", fails: true) }).GetAwaiter().GetResult();
            Assert.AreEqual(PlatformId.Offline, outcome.Platform.Id);
            Assert.AreEqual(PlatformStatus.Ready, outcome.Platform.Status);
        }
    }
}
