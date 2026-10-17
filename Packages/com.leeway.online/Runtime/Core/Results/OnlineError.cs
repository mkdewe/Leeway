using System;

namespace Leeway.Online
{
    /// <summary>A failure with a code to branch on and a message meant for logs (not for players).</summary>
    public readonly struct OnlineError : IEquatable<OnlineError>
    {
        public static readonly OnlineError None = default;

        public readonly OnlineErrorCode Code;
        public readonly string Message;

        public OnlineError(OnlineErrorCode code, string message = null)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool IsError => Code != OnlineErrorCode.None;

        public static OnlineError Cancelled => new(OnlineErrorCode.Cancelled, "The operation was cancelled.");
        public static OnlineError NotSupported(string what) => new(OnlineErrorCode.NotSupported, $"{what} is not supported by this platform.");
        public static OnlineError NotInLobby => new(OnlineErrorCode.NotInLobby, "Not in a lobby.");
        public static OnlineError NotOwner => new(OnlineErrorCode.NotLobbyOwner, "Only the lobby owner can do that.");

        public bool Equals(OnlineError other) => Code == other.Code && Message == other.Message;
        public override bool Equals(object obj) => obj is OnlineError other && Equals(other);
        public override int GetHashCode() => ((int)Code * 397) ^ (Message?.GetHashCode() ?? 0);
        public override string ToString() => string.IsNullOrEmpty(Message) ? Code.ToString() : $"{Code}: {Message}";
    }
}
