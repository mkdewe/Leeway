using System.Threading;
using Cysharp.Threading.Tasks;
using Steamworks;

namespace Leeway.Online.Steam
{
    /// <summary>Adapts Steam's call-result callbacks to awaitable UniTasks.</summary>
    /// <remarks>
    /// Cancellation is reported as an outcome, not thrown: the online layer's contract is that
    /// expected failures — the player pressing Cancel included — come back as results.
    /// </remarks>
    internal static class SteamCallResults
    {
        public readonly struct Outcome<T>
        {
            public readonly T Result;
            public readonly bool IOFailure;
            public readonly bool Cancelled;

            public Outcome(T result, bool ioFailure, bool cancelled)
            {
                Result = result;
                IOFailure = ioFailure;
                Cancelled = cancelled;
            }

            /// <summary>The transport-level failure, if any; <see cref="OnlineError.None"/> when <see cref="Result"/> is meaningful.</summary>
            public OnlineError Error(string what) =>
                Cancelled ? OnlineError.Cancelled
                : IOFailure ? new OnlineError(OnlineErrorCode.ServiceError, $"{what}: Steam I/O failure.")
                : OnlineError.None;
        }

        public static async UniTask<Outcome<T>> Await<T>(SteamAPICall_t call, CancellationToken cancellationToken) where T : struct
        {
            if (call == SteamAPICall_t.Invalid) return new Outcome<T>(default, ioFailure: true, cancelled: false);
            if (cancellationToken.IsCancellationRequested) return new Outcome<T>(default, ioFailure: false, cancelled: true);

            var completion = new UniTaskCompletionSource<Outcome<T>>();
            var callResult = CallResult<T>.Create((result, ioFailure) => completion.TrySetResult(new Outcome<T>(result, ioFailure, cancelled: false)));
            callResult.Set(call);

            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    callResult.Cancel();
                    completion.TrySetResult(new Outcome<T>(default, ioFailure: false, cancelled: true));
                });
            }

            try
            {
                return await completion.Task;
            }
            finally
            {
                registration.Dispose();
                callResult.Dispose();
            }
        }
    }
}
