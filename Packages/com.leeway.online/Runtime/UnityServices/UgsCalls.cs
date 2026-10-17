using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;

namespace Leeway.Online.UnityServices
{
    /// <summary>
    /// Adapter from the UGS SDK's Task-and-exception style to <see cref="OnlineResult"/>: every call
    /// goes through here, so nothing above the backend ever sees a <see cref="RequestFailedException"/>.
    /// </summary>
    internal static class UgsCalls
    {
        public static async UniTask<OnlineResult<T>> Run<T>(Task<T> call, string what, CancellationToken cancellationToken)
        {
            try
            {
                return OnlineResult<T>.Success(await call.AsUniTask().AttachExternalCancellation(cancellationToken));
            }
            catch (Exception exception)
            {
                return OnlineResult<T>.Failure(ToError(exception, what));
            }
        }

        public static async UniTask<OnlineResult> Run(Task call, string what, CancellationToken cancellationToken)
        {
            try
            {
                await call.AsUniTask().AttachExternalCancellation(cancellationToken);
                return OnlineResult.Success;
            }
            catch (Exception exception)
            {
                return OnlineResult.Failure(ToError(exception, what));
            }
        }

        public static OnlineError ToError(Exception exception, string what) => exception switch
        {
            OperationCanceledException => OnlineError.Cancelled,
            TimeoutException => new OnlineError(OnlineErrorCode.Timeout, $"{what}: no answer from Unity Gaming Services."),
            LobbyServiceException lobby => new OnlineError(Map(lobby.Reason), $"{what}: {lobby.Reason} — {lobby.Message}"),
            AuthenticationException auth => new OnlineError(OnlineErrorCode.AccessDenied, $"{what}: sign-in failed ({auth.ErrorCode}) — {auth.Message}"),
            RequestFailedException request => new OnlineError(OnlineErrorCode.ServiceError, $"{what}: request failed ({request.ErrorCode}) — {request.Message}"),
            ServicesInitializationException init => new OnlineError(OnlineErrorCode.PlatformUnavailable, $"{what}: {init.Message}"),
            _ => new OnlineError(OnlineErrorCode.Unknown, $"{what}: {exception.GetType().Name} — {exception.Message}"),
        };

        private static OnlineErrorCode Map(LobbyExceptionReason reason) => reason switch
        {
            LobbyExceptionReason.LobbyNotFound or LobbyExceptionReason.EntityNotFound or LobbyExceptionReason.PlayerNotFound => OnlineErrorCode.NotFound,
            LobbyExceptionReason.LobbyFull => OnlineErrorCode.LobbyFull,
            LobbyExceptionReason.LobbyLocked or LobbyExceptionReason.Forbidden or LobbyExceptionReason.IncorrectPassword => OnlineErrorCode.AccessDenied,
            LobbyExceptionReason.Unauthorized => OnlineErrorCode.NotInitialized,
            LobbyExceptionReason.InvalidJoinCode or LobbyExceptionReason.ValidationError or LobbyExceptionReason.BadRequest => OnlineErrorCode.InvalidArgument,
            LobbyExceptionReason.NoOpenLobbies => OnlineErrorCode.NotFound,
            LobbyExceptionReason.RequestTimeOut or LobbyExceptionReason.GatewayTimeout => OnlineErrorCode.Timeout,
            LobbyExceptionReason.NetworkError or LobbyExceptionReason.LobbyEventServiceConnectionError => OnlineErrorCode.ConnectionFailed,
            _ => OnlineErrorCode.ServiceError,
        };
    }
}
