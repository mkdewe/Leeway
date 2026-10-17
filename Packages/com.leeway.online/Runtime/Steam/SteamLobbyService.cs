using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Steamworks;

namespace Leeway.Online.Steam
{
    /// <summary>
    /// ISteamMatchmaking behind <see cref="ILobbyService"/>: call results become awaitables, and the
    /// lobby callbacks are routed to the current <see cref="SteamLobby"/>.
    /// </summary>
    internal sealed class SteamLobbyService : LobbyServiceBase, IDisposable
    {
        private const string ConnectLobbyArgument = "+connect_lobby";

        private readonly CSteamID _localUser;
        private readonly List<IDisposable> _callbacks = new();

        public SteamLobbyService(CSteamID localUser)
        {
            _localUser = localUser;

            _callbacks.Add(Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate));
            _callbacks.Add(Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate));
            _callbacks.Add(Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage));
            _callbacks.Add(Callback<LobbyKicked_t>.Create(OnLobbyKicked));
            _callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested));

            QueueLaunchArgumentJoin();
        }

        private SteamLobby Lobby => CurrentLobby as SteamLobby;

        public override async UniTask<OnlineResult<ILobby>> CreateAsync(LobbyCreateOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null) return OnlineResult<ILobby>.Failure(OnlineErrorCode.InvalidArgument, "No lobby options.");

            Leave();

            var call = SteamMatchmaking.CreateLobby(options.Visibility.ToSteam(), options.MaxMembers);
            var outcome = await SteamCallResults.Await<LobbyCreated_t>(call, cancellationToken);
            if (outcome.Error("Create lobby") is { IsError: true } createFailure) return OnlineResult<ILobby>.Failure(createFailure);

            var error = outcome.Result.m_eResult.ToError("Create lobby");
            if (error.IsError) return OnlineResult<ILobby>.Failure(error);

            var steamLobby = new CSteamID(outcome.Result.m_ulSteamIDLobby);

            // The owner writes everything before anyone can search for it — on Steam a lobby is only
            // listed once it has data, so there is no window where it shows up nameless.
            foreach (var pair in options.Data) SteamMatchmaking.SetLobbyData(steamLobby, pair.Key, pair.Value);
            SteamMatchmaking.SetLobbyData(steamLobby, LobbyDataKeys.Visibility, options.Visibility.ToString());

            var lobby = new SteamLobby(steamLobby, _localUser, options.Visibility);
            Enter(lobby);
            return OnlineResult<ILobby>.Success(lobby);
        }

        public override async UniTask<OnlineResult<ILobby>> JoinAsync(LobbyId lobbyId, CancellationToken cancellationToken = default)
        {
            if (!lobbyId.TryToSteamId(out var steamLobby) || !steamLobby.IsLobby())
                return OnlineResult<ILobby>.Failure(OnlineErrorCode.InvalidArgument, $"'{lobbyId}' is not a Steam lobby id.");

            if (Lobby != null && Lobby.SteamId == steamLobby) return OnlineResult<ILobby>.Success(Lobby);

            Leave();

            var outcome = await SteamCallResults.Await<LobbyEnter_t>(SteamMatchmaking.JoinLobby(steamLobby), cancellationToken);

            // A cancelled join may still complete on Steam's side — make sure we do not linger there.
            if (outcome.Cancelled) SteamMatchmaking.LeaveLobby(steamLobby);
            if (outcome.Error("Join lobby") is { IsError: true } joinFailure) return OnlineResult<ILobby>.Failure(joinFailure);

            var error = ((EChatRoomEnterResponse)outcome.Result.m_EChatRoomEnterResponse).ToError();
            if (error.IsError) return OnlineResult<ILobby>.Failure(error);

            var lobby = new SteamLobby(steamLobby, _localUser);
            Enter(lobby);
            return OnlineResult<ILobby>.Success(lobby);
        }

        public override async UniTask<OnlineResult<IReadOnlyList<LobbySummary>>> SearchAsync(LobbyQuery query, CancellationToken cancellationToken = default)
        {
            query ??= LobbyQuery.Create().Build();

            // Filters are per-request global state in Steam: they apply to the next RequestLobbyList only.
            foreach (var filter in query.Filters)
            {
                if (filter.IsNumeric) SteamMatchmaking.AddRequestLobbyListNumericalFilter(filter.Key, filter.Number.Value, filter.Comparison.ToSteam());
                else SteamMatchmaking.AddRequestLobbyListStringFilter(filter.Key, filter.Text, filter.Comparison.ToSteam());
            }
            if (query.MinFreeSlots > 0) SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(query.MinFreeSlots);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(query.Distance.ToSteam());
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(query.MaxResults);

            var outcome = await SteamCallResults.Await<LobbyMatchList_t>(SteamMatchmaking.RequestLobbyList(), cancellationToken);
            if (outcome.Error("Lobby search") is { IsError: true } searchFailure) return OnlineResult<IReadOnlyList<LobbySummary>>.Failure(searchFailure);

            int count = (int)outcome.Result.m_nLobbiesMatching;
            var found = new List<LobbySummary>(count);
            for (int i = 0; i < count; i++)
            {
                var steamLobby = SteamMatchmaking.GetLobbyByIndex(i);
                var summary = new LobbySummary(
                    steamLobby.ToLobbyId(),
                    SteamMatchmaking.GetNumLobbyMembers(steamLobby),
                    SteamMatchmaking.GetLobbyMemberLimit(steamLobby),
                    ReadAllData(steamLobby));

                // Steam's string comparisons are equality-only in practice; re-check on the client so
                // every backend honours the same query semantics.
                if (query.Matches(summary)) found.Add(summary);
            }
            return OnlineResult<IReadOnlyList<LobbySummary>>.Success(found);
        }

        protected override void NotifyBackendOfLeave(LobbyBase lobby) => SteamMatchmaking.LeaveLobby(((SteamLobby)lobby).SteamId);

        public void Dispose()
        {
            Leave();
            foreach (var callback in _callbacks) callback.Dispose();
            _callbacks.Clear();
        }

        private static Dictionary<string, string> ReadAllData(CSteamID lobby)
        {
            int count = SteamMatchmaking.GetLobbyDataCount(lobby);
            var data = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                if (SteamMatchmaking.GetLobbyDataByIndex(lobby, i, out var key, Constants.k_nMaxLobbyKeyLength, out var value, Constants.k_cubChatMetadataMax))
                    data[key] = value;
            }
            return data;
        }

        private bool IsCurrent(ulong lobbyId) => Lobby != null && Lobby.SteamId.m_SteamID == lobbyId;

        private void OnLobbyChatUpdate(LobbyChatUpdate_t e)
        {
            if (!IsCurrent(e.m_ulSteamIDLobby)) return;

            var user = new CSteamID(e.m_ulSteamIDUserChanged);
            var change = (EChatMemberStateChange)e.m_rgfChatMemberStateChange;

            if ((change & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                Lobby.HandleMemberEntered(user);
            }
            else
            {
                var reason = (change & (EChatMemberStateChange.k_EChatMemberStateChangeKicked | EChatMemberStateChange.k_EChatMemberStateChangeBanned)) != 0
                    ? LobbyLeaveReason.Kicked
                    : (change & EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) != 0
                        ? LobbyLeaveReason.Disconnected
                        : LobbyLeaveReason.Left;

                if (user == _localUser)
                {
                    Exit(reason);
                    return;
                }
                Lobby.HandleMemberLeft(user, reason);
            }

            // Steam migrates ownership silently when the owner leaves; this is where it shows.
            Lobby.HandleOwnerMaybeChanged();
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t e)
        {
            if (!IsCurrent(e.m_ulSteamIDLobby) || e.m_bSuccess == 0) return;

            if (e.m_ulSteamIDMember == e.m_ulSteamIDLobby)
            {
                Lobby.HandleOwnerMaybeChanged();
                Lobby.HandleDataChanged();
            }
            else
            {
                Lobby.HandleMemberDataChanged(new CSteamID(e.m_ulSteamIDMember));
            }
        }

        private void OnLobbyChatMessage(LobbyChatMsg_t e)
        {
            if (IsCurrent(e.m_ulSteamIDLobby)) Lobby.HandleChat((int)e.m_iChatID);
        }

        private void OnLobbyKicked(LobbyKicked_t e)
        {
            if (!IsCurrent(e.m_ulSteamIDLobby)) return;
            Exit(e.m_bKickedDueToDisconnect != 0 ? LobbyLeaveReason.Disconnected : LobbyLeaveReason.Kicked);
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t e) =>
            RaiseJoinRequest(new LobbyJoinRequest(e.m_steamIDLobby.ToLobbyId(), e.m_steamIDFriend.ToUserId(), LobbyJoinRequestSource.PlatformOverlay));

        /// <summary>
        /// Steam starts the game with <c>+connect_lobby &lt;id&gt;</c> when a player accepts an invite
        /// while the game is not running.
        /// </summary>
        private void QueueLaunchArgumentJoin()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], ConnectLobbyArgument, StringComparison.OrdinalIgnoreCase)) continue;
                var id = new LobbyId(args[i + 1]);
                if (id.TryToSteamId(out _))
                    RaiseJoinRequest(new LobbyJoinRequest(id, UserId.None, LobbyJoinRequestSource.LaunchArguments));
                return;
            }
        }
    }
}
