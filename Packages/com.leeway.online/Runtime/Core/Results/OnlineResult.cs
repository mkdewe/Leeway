namespace Leeway.Online
{
    /// <summary>
    /// The outcome of an online operation without a value.
    /// </summary>
    /// <remarks>
    /// Online calls fail as a matter of course — the lobby filled up, Steam dropped, the player
    /// cancelled — so failures are values to branch on rather than exceptions. Exceptions stay reserved
    /// for programming errors.
    /// </remarks>
    public readonly struct OnlineResult
    {
        public static readonly OnlineResult Success = default;

        public readonly OnlineError Error;

        private OnlineResult(OnlineError error) => Error = error;

        public bool IsSuccess => !Error.IsError;

        public static OnlineResult Failure(OnlineError error) => new(error);
        public static OnlineResult Failure(OnlineErrorCode code, string message = null) => new(new OnlineError(code, message));

        public static implicit operator OnlineResult(OnlineError error) => new(error);

        public override string ToString() => IsSuccess ? "Success" : Error.ToString();
    }

    /// <summary>The outcome of an online operation that yields a value on success.</summary>
    public readonly struct OnlineResult<T>
    {
        public readonly T Value;
        public readonly OnlineError Error;

        private OnlineResult(T value, OnlineError error)
        {
            Value = value;
            Error = error;
        }

        public bool IsSuccess => !Error.IsError;

        public bool TryGetValue(out T value)
        {
            value = Value;
            return IsSuccess;
        }

        public static OnlineResult<T> Success(T value) => new(value, OnlineError.None);
        public static OnlineResult<T> Failure(OnlineError error) => new(default, error);
        public static OnlineResult<T> Failure(OnlineErrorCode code, string message = null) => new(default, new OnlineError(code, message));

        public static implicit operator OnlineResult<T>(OnlineError error) => new(default, error);

        /// <summary>Drops the value — for callers that only care whether it worked.</summary>
        public OnlineResult AsResult() => IsSuccess ? OnlineResult.Success : OnlineResult.Failure(Error);

        public override string ToString() => IsSuccess ? $"Success({Value})" : Error.ToString();
    }
}
