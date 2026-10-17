using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Leeway.Online
{
    /// <summary>
    /// Picks the first backend that actually comes up, in the order the game prefers them.
    /// </summary>
    /// <remarks>
    /// <para>A chain of responsibility over <see cref="OnlinePlatformProvider"/>s: "Steam, else Local"
    /// on PC, "PSN, else Offline" on a console. A candidate that throws or fails to initialize is
    /// disposed and the next one is tried, so a missing Steam client degrades to an offline game
    /// instead of a dead main menu.</para>
    ///
    /// <para>The chain always ends with <see cref="OfflinePlatform"/>, so the result is never null.</para>
    /// </remarks>
    public static class OnlinePlatformBootstrapper
    {
        public readonly struct Attempt
        {
            public readonly PlatformId Platform;
            public readonly OnlineError Error;

            public Attempt(PlatformId platform, OnlineError error)
            {
                Platform = platform;
                Error = error;
            }

            public override string ToString() => $"{Platform}: {Error}";
        }

        public readonly struct Outcome
        {
            public readonly IOnlinePlatform Platform;

            /// <summary>The candidates that were tried and failed before <see cref="Platform"/>, in order.</summary>
            public readonly IReadOnlyList<Attempt> FailedAttempts;

            public Outcome(IOnlinePlatform platform, IReadOnlyList<Attempt> failedAttempts)
            {
                Platform = platform;
                FailedAttempts = failedAttempts;
            }
        }

        public static async UniTask<Outcome> StartFirstAvailableAsync(
            IEnumerable<OnlinePlatformProvider> providers,
            CancellationToken cancellationToken = default)
        {
            var failures = new List<Attempt>();

            if (providers != null)
            {
                foreach (var provider in providers)
                {
                    // A provider asset whose backend assembly is not compiled for this target
                    // deserializes as null — that is the intended way to switch a backend off per platform.
                    if (provider == null || !provider.IsSupportedOnCurrentRuntime) continue;

                    cancellationToken.ThrowIfCancellationRequested();

                    IOnlinePlatform platform = null;
                    OnlineResult result;
                    try
                    {
                        platform = provider.CreatePlatform();
                        result = await platform.InitializeAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        platform?.Dispose();
                        throw;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        result = OnlineResult.Failure(OnlineErrorCode.Unknown, exception.Message);
                    }

                    if (result.IsSuccess) return new Outcome(platform, failures);

                    failures.Add(new Attempt(provider.PlatformId, result.Error));
                    platform?.Dispose();
                }
            }

            var offline = new OfflinePlatform();
            await offline.InitializeAsync(cancellationToken);
            return new Outcome(offline, failures);
        }
    }
}
