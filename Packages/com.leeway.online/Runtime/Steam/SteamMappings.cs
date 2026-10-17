using Steamworks;

namespace Leeway.Online.Steam
{
    /// <summary>Translations between Steam's vocabulary and the backend-neutral one.</summary>
    internal static class SteamMappings
    {
        public static UserId ToUserId(this CSteamID id) => new(id.m_SteamID.ToString());
        public static LobbyId ToLobbyId(this CSteamID id) => new(id.m_SteamID.ToString());

        public static bool TryToSteamId(this UserId id, out CSteamID steamId) => TryParse(id.Value, out steamId);
        public static bool TryToSteamId(this LobbyId id, out CSteamID steamId) => TryParse(id.Value, out steamId);

        private static bool TryParse(string value, out CSteamID steamId)
        {
            steamId = CSteamID.Nil;
            if (!ulong.TryParse(value, out ulong raw)) return false;
            steamId = new CSteamID(raw);
            return steamId.IsValid();
        }

        public static ELobbyType ToSteam(this LobbyVisibility visibility) => visibility switch
        {
            LobbyVisibility.Public => ELobbyType.k_ELobbyTypePublic,
            LobbyVisibility.FriendsOnly => ELobbyType.k_ELobbyTypeFriendsOnly,
            _ => ELobbyType.k_ELobbyTypePrivate,
        };

        public static ELobbyComparison ToSteam(this LobbyFilterComparison comparison) => comparison switch
        {
            LobbyFilterComparison.Equal => ELobbyComparison.k_ELobbyComparisonEqual,
            LobbyFilterComparison.NotEqual => ELobbyComparison.k_ELobbyComparisonNotEqual,
            LobbyFilterComparison.LessThan => ELobbyComparison.k_ELobbyComparisonLessThan,
            LobbyFilterComparison.LessThanOrEqual => ELobbyComparison.k_ELobbyComparisonEqualToOrLessThan,
            LobbyFilterComparison.GreaterThan => ELobbyComparison.k_ELobbyComparisonGreaterThan,
            _ => ELobbyComparison.k_ELobbyComparisonEqualToOrGreaterThan,
        };

        public static ELobbyDistanceFilter ToSteam(this LobbySearchDistance distance) => distance switch
        {
            LobbySearchDistance.Close => ELobbyDistanceFilter.k_ELobbyDistanceFilterClose,
            LobbySearchDistance.Far => ELobbyDistanceFilter.k_ELobbyDistanceFilterFar,
            LobbySearchDistance.Worldwide => ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide,
            _ => ELobbyDistanceFilter.k_ELobbyDistanceFilterDefault,
        };

        public static OnlineError ToError(this EResult result, string what) => result switch
        {
            EResult.k_EResultOK => OnlineError.None,
            EResult.k_EResultTimeout => new OnlineError(OnlineErrorCode.Timeout, $"{what}: Steam timed out."),
            EResult.k_EResultNoConnection => new OnlineError(OnlineErrorCode.PlatformUnavailable, $"{what}: not connected to Steam."),
            EResult.k_EResultLimitExceeded => new OnlineError(OnlineErrorCode.ServiceError, $"{what}: too many lobbies."),
            EResult.k_EResultAccessDenied => new OnlineError(OnlineErrorCode.AccessDenied, $"{what}: access denied."),
            _ => new OnlineError(OnlineErrorCode.ServiceError, $"{what}: Steam returned {result}."),
        };

        public static OnlineError ToError(this EChatRoomEnterResponse response) => response switch
        {
            EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess => OnlineError.None,
            EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist => new OnlineError(OnlineErrorCode.NotFound, "The lobby no longer exists."),
            EChatRoomEnterResponse.k_EChatRoomEnterResponseFull => new OnlineError(OnlineErrorCode.LobbyFull, "The lobby is full."),
            EChatRoomEnterResponse.k_EChatRoomEnterResponseRatelimitExceeded => new OnlineError(OnlineErrorCode.ServiceError, "Too many join attempts — try again in a moment."),
            EChatRoomEnterResponse.k_EChatRoomEnterResponseError => new OnlineError(OnlineErrorCode.ServiceError, "Steam could not join the lobby."),
            _ => new OnlineError(OnlineErrorCode.AccessDenied, $"Steam refused to join the lobby ({response})."),
        };
    }
}
