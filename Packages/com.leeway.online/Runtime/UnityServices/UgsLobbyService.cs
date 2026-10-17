using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Leeway.Online.UnityServices
{
    /// <summary>
    /// The UGS Lobby service behind <see cref="ILobbyService"/>.
    /// </summary>
    /// <remarks>
    /// <para>While the local user is in a lobby the service keeps it alive and current:</para>
    /// <list type="bullet">
    /// <item>lobby events (Wire) deliver changes as they happen;</item>
    /// <item>a poll catches anything missed — fast while events are not connected, slow otherwise;</item>
    /// <item>the owner sends the heartbeat UGS needs to keep a lobby listed (it goes inactive after 30 s);</item>
    /// <item>queued writes are flushed once per frame (see <see cref="UgsLobby"/>).</item>
    /// </list>
    /// </remarks>
    internal sealed class UgsLobbyService : LobbyServiceBase, IDisposable
    {
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan FastPollInterval = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan SlowPollInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan WriteRetryDelay = TimeSpan.FromSeconds(1.5);
        private const int MaxWriteAttempts = 3;

        // UGS lobby codes are six characters; lobby ids are much longer.
        private const int MaxJoinCodeLength = 8;

        private readonly UserProfile _localUser;
        private readonly UgsDataIndex _index;

        private CancellationTokenSource _tracking;
        private ILobbyEvents _events;
        private LobbyEventConnectionState _eventsState = LobbyEventConnectionState.Unsubscribed;
        private bool _flushScheduled;

        public UgsLobbyService(UserProfile localUser, UgsDataIndex index)
        {
            _localUser = localUser;
            _index = index;
        }

        private static Unity.Services.Lobbies.ILobbyService Api => LobbyService.Instance;
        private UgsLobby Joined => CurrentLobby as UgsLobby;

        public override async UniTask<OnlineResult<ILobby>> CreateAsync(LobbyCreateOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null) return OnlineResult<ILobby>.Failure(OnlineErrorCode.InvalidArgument, "No lobby options.");

            Leave();

            var data = new Dictionary<string, DataObject>();
            foreach (var pair in options.Data) data[pair.Key] = PublicData(pair.Key, pair.Value);
            data[LobbyDataKeys.Visibility] = PublicData(LobbyDataKeys.Visibility, options.Visibility.ToString());

            var request = new CreateLobbyOptions
            {
                IsPrivate = options.Visibility != LobbyVisibility.Public,
                Data = data,
                Player = LocalPlayer(),
            };

            var call = Api.CreateLobbyAsync(options.Name, options.MaxMembers, request);
            var created = await UgsCalls.Run(call, "Create lobby", cancellationToken);
            if (!created.IsSuccess)
            {
                if (created.Error.Code == OnlineErrorCode.Cancelled) AbandonWhenDone(call, deleteIfOwner: true).Forget();
                return OnlineResult<ILobby>.Failure(created.Error);
            }

            return OnlineResult<ILobby>.Success(Track(created.Value));
        }

        public override async UniTask<OnlineResult<ILobby>> JoinAsync(LobbyId lobbyId, CancellationToken cancellationToken = default)
        {
            if (!lobbyId.IsValid) return OnlineResult<ILobby>.Failure(OnlineErrorCode.InvalidArgument, "No lobby id or code.");

            if (Joined != null && (Joined.Id == lobbyId || string.Equals(Joined.JoinCode, lobbyId.Value, StringComparison.OrdinalIgnoreCase)))
                return OnlineResult<ILobby>.Success(Joined);

            Leave();

            var call = lobbyId.Value.Length <= MaxJoinCodeLength
                ? Api.JoinLobbyByCodeAsync(lobbyId.Value.ToUpperInvariant(), new JoinLobbyByCodeOptions { Player = LocalPlayer() })
                : Api.JoinLobbyByIdAsync(lobbyId.Value, new JoinLobbyByIdOptions { Player = LocalPlayer() });

            var joined = await UgsCalls.Run(call, "Join lobby", cancellationToken);
            if (!joined.IsSuccess)
            {
                // A cancelled join may still complete on the service — make sure we do not linger there.
                if (joined.Error.Code == OnlineErrorCode.Cancelled) AbandonWhenDone(call, deleteIfOwner: false).Forget();
                return OnlineResult<ILobby>.Failure(joined.Error);
            }

            return OnlineResult<ILobby>.Success(Track(joined.Value));
        }

        public override async UniTask<OnlineResult<IReadOnlyList<LobbySummary>>> SearchAsync(LobbyQuery query, CancellationToken cancellationToken = default)
        {
            query ??= LobbyQuery.Create().Build();

            var filters = new List<QueryFilter>();
            if (query.MinFreeSlots > 0)
                filters.Add(new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, query.MinFreeSlots.ToString(), QueryFilter.OpOptions.GE));

            // Only equality on indexed keys runs on the service; the full query is re-checked below.
            foreach (var filter in query.Filters)
            {
                if (filter.IsNumeric || !_index.TryGetField(filter.Key, out var field)) continue;
                if (filter.Comparison == LobbyFilterComparison.Equal) filters.Add(new QueryFilter(field, filter.Text, QueryFilter.OpOptions.EQ));
                else if (filter.Comparison == LobbyFilterComparison.NotEqual) filters.Add(new QueryFilter(field, filter.Text, QueryFilter.OpOptions.NE));
            }

            var request = new QueryLobbiesOptions { Count = Mathf.Clamp(query.MaxResults, 1, 100), Filters = filters };
            var response = await UgsCalls.Run(Api.QueryLobbiesAsync(request), "Lobby search", cancellationToken);
            if (!response.IsSuccess) return OnlineResult<IReadOnlyList<LobbySummary>>.Failure(response.Error);

            var results = response.Value.Results ?? new List<Lobby>();
            var found = new List<LobbySummary>(results.Count);
            foreach (var lobby in results)
            {
                var data = new Dictionary<string, string>();
                if (lobby.Data != null)
                    foreach (var pair in lobby.Data) data[pair.Key] = pair.Value.Value;

                var summary = new LobbySummary(new LobbyId(lobby.Id), lobby.MaxPlayers - lobby.AvailableSlots, lobby.MaxPlayers, data);
                if (query.Matches(summary)) found.Add(summary);
            }
            return OnlineResult<IReadOnlyList<LobbySummary>>.Success(found);
        }

        protected override void NotifyBackendOfLeave(LobbyBase lobby)
        {
            StopTracking();
            RemoveLocalPlayerAsync(((UgsLobby)lobby).UgsId).Forget();
        }

        public void Dispose()
        {
            Leave();
            StopTracking();
        }

        // ---- Tracking the current lobby ----------------------------------------------------------

        private UgsLobby Track(Lobby model)
        {
            var lobby = new UgsLobby(model, _localUser.Id, _index, RequestFlush);
            Enter(lobby);

            _tracking = new CancellationTokenSource();
            var token = _tracking.Token;
            SubscribeAsync(lobby, token).Forget();
            PollLoopAsync(lobby, token).Forget();
            HeartbeatLoopAsync(lobby, token).Forget();

            Debug.Log($"[Online] UGS lobby {lobby.Id} (code {lobby.JoinCode}) entered — {lobby.Members.Count}/{lobby.MaxMembers} members.");
            return lobby;
        }

        private void StopTracking()
        {
            if (_tracking != null)
            {
                _tracking.Cancel();
                _tracking.Dispose();
                _tracking = null;
            }

            if (_events != null)
            {
                var events = _events;
                _events = null;
                UgsCalls.Run(events.UnsubscribeAsync(), "Lobby events", CancellationToken.None).Forget();
            }

            _eventsState = LobbyEventConnectionState.Unsubscribed;
            _flushScheduled = false;
        }

        private bool IsCurrent(UgsLobby lobby, CancellationToken token) => !token.IsCancellationRequested && ReferenceEquals(Joined, lobby);

        /// <summary>The local user is out without having asked (kicked, lobby gone).</summary>
        private void Drop(UgsLobby lobby, LobbyLeaveReason reason)
        {
            if (!ReferenceEquals(Joined, lobby)) return;
            StopTracking();
            Exit(reason);
        }

        private async UniTaskVoid SubscribeAsync(UgsLobby lobby, CancellationToken token)
        {
            var callbacks = new LobbyEventCallbacks();
            callbacks.LobbyChanged += changes =>
            {
                if (!IsCurrent(lobby, token)) return;
                if (changes.LobbyDeleted)
                {
                    Drop(lobby, LobbyLeaveReason.LobbyClosed);
                    return;
                }
                changes.ApplyToLobby(lobby.Model);
                Refresh(lobby, lobby.Model);
            };
            callbacks.KickedFromLobby += () =>
            {
                if (IsCurrent(lobby, token)) Drop(lobby, LobbyLeaveReason.Kicked);
            };
            callbacks.LobbyEventConnectionStateChanged += state =>
            {
                if (IsCurrent(lobby, token)) _eventsState = state;
            };

            _eventsState = LobbyEventConnectionState.Subscribing;
            var subscribed = await UgsCalls.Run(Api.SubscribeToLobbyEventsAsync(lobby.UgsId, callbacks), "Lobby events", token);

            if (!IsCurrent(lobby, token))
            {
                if (subscribed.IsSuccess) UgsCalls.Run(subscribed.Value.UnsubscribeAsync(), "Lobby events", CancellationToken.None).Forget();
                return;
            }

            if (subscribed.IsSuccess)
            {
                _events = subscribed.Value;
            }
            else
            {
                // Polling carries the lobby on its own; events only make it snappier.
                _eventsState = LobbyEventConnectionState.Error;
                Debug.LogWarning($"[Online] UGS lobby events unavailable, polling instead. {subscribed.Error}");
            }
        }

        private async UniTaskVoid PollLoopAsync(UgsLobby lobby, CancellationToken token)
        {
            while (IsCurrent(lobby, token))
            {
                var interval = _eventsState == LobbyEventConnectionState.Subscribed ? SlowPollInterval : FastPollInterval;
                if (await UniTask.Delay(interval, DelayType.Realtime, cancellationToken: token).SuppressCancellationThrow()) return;

                // A queued write would be overwritten by the older server state; let it land first.
                if (_flushScheduled || lobby.HasPendingLobbyWrite || lobby.HasPendingMemberWrite) continue;

                var fetched = await UgsCalls.Run(Api.GetLobbyAsync(lobby.UgsId), "Lobby refresh", token);
                if (!IsCurrent(lobby, token)) return;

                if (fetched.IsSuccess) Refresh(lobby, fetched.Value);
                else if (fetched.Error.Code == OnlineErrorCode.NotFound) Drop(lobby, LobbyLeaveReason.LobbyClosed);
                else if (fetched.Error.Code is OnlineErrorCode.AccessDenied) Drop(lobby, LobbyLeaveReason.Kicked);
                else Debug.LogWarning($"[Online] {fetched.Error}");
            }
        }

        private async UniTaskVoid HeartbeatLoopAsync(UgsLobby lobby, CancellationToken token)
        {
            while (IsCurrent(lobby, token))
            {
                if (lobby.IsLocalUserOwner)
                {
                    var ping = await UgsCalls.Run(Api.SendHeartbeatPingAsync(lobby.UgsId), "Lobby heartbeat", token);
                    if (!ping.IsSuccess && ping.Error.Code != OnlineErrorCode.Cancelled) Debug.LogWarning($"[Online] {ping.Error}");
                }

                if (await UniTask.Delay(HeartbeatInterval, DelayType.Realtime, cancellationToken: token).SuppressCancellationThrow()) return;
            }
        }

        /// <summary>A newer model arrived. The local user missing from it means they were removed.</summary>
        private void Refresh(UgsLobby lobby, Lobby model)
        {
            bool stillMember = false;
            if (model.Players != null)
                foreach (var player in model.Players)
                    if (player.Id == _localUser.Id.Value) { stillMember = true; break; }

            if (!stillMember)
            {
                Drop(lobby, LobbyLeaveReason.Kicked);
                return;
            }
            lobby.Apply(model);
        }

        // ---- Writes ------------------------------------------------------------------------------

        private void RequestFlush(UgsLobby lobby)
        {
            if (_flushScheduled || _tracking == null) return;
            _flushScheduled = true;
            FlushAsync(lobby, _tracking.Token).Forget();
        }

        private async UniTaskVoid FlushAsync(UgsLobby lobby, CancellationToken token)
        {
            // Everything written this frame goes out together.
            if (await UniTask.NextFrame(token).SuppressCancellationThrow()) return;

            for (int attempt = 1; IsCurrent(lobby, token); attempt++)
            {
                var result = await SendPendingAsync(lobby, token);
                if (!IsCurrent(lobby, token)) return;
                if (result.IsSuccess || attempt >= MaxWriteAttempts || result.Error.Code is OnlineErrorCode.AccessDenied or OnlineErrorCode.InvalidArgument)
                {
                    if (!result.IsSuccess)
                    {
                        Debug.LogError($"[Online] Lobby update dropped after {attempt} attempt(s). {result.Error}");
                        lobby.PendingData.Clear();
                        lobby.PendingMemberData.Clear();
                        lobby.PendingLocked = null;
                    }
                    break;
                }

                Debug.LogWarning($"[Online] Lobby update failed, retrying. {result.Error}");
                if (await UniTask.Delay(WriteRetryDelay, DelayType.Realtime, cancellationToken: token).SuppressCancellationThrow()) return;
            }

            _flushScheduled = false;

            // Something was written while the request was in flight.
            if (IsCurrent(lobby, token) && (lobby.HasPendingLobbyWrite || lobby.HasPendingMemberWrite)) RequestFlush(lobby);
        }

        private async UniTask<OnlineResult> SendPendingAsync(UgsLobby lobby, CancellationToken token)
        {
            if (lobby.HasPendingLobbyWrite)
            {
                var data = new Dictionary<string, DataObject>(lobby.PendingData);
                bool? locked = lobby.PendingLocked;

                var request = new UpdateLobbyOptions { Data = data.Count > 0 ? data : null, IsLocked = locked };
                var updated = await UgsCalls.Run(Api.UpdateLobbyAsync(lobby.UgsId, request), "Update lobby", token);
                if (!updated.IsSuccess) return updated.AsResult();

                // Only what was sent is confirmed; a newer write to the same key stays queued.
                foreach (var pair in data)
                    if (lobby.PendingData.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value))
                        lobby.PendingData.Remove(pair.Key);
                if (lobby.PendingLocked == locked) lobby.PendingLocked = null;

                if (IsCurrent(lobby, token)) Refresh(lobby, updated.Value);
            }

            if (lobby.HasPendingMemberWrite && IsCurrent(lobby, token))
            {
                var data = new Dictionary<string, PlayerDataObject>(lobby.PendingMemberData);
                var request = new UpdatePlayerOptions { Data = data };
                var updated = await UgsCalls.Run(Api.UpdatePlayerAsync(lobby.UgsId, _localUser.Id.Value, request), "Update member data", token);
                if (!updated.IsSuccess) return updated.AsResult();

                foreach (var pair in data)
                    if (lobby.PendingMemberData.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value))
                        lobby.PendingMemberData.Remove(pair.Key);

                if (IsCurrent(lobby, token)) Refresh(lobby, updated.Value);
            }

            return OnlineResult.Success;
        }

        // ---- Helpers -----------------------------------------------------------------------------

        private Player LocalPlayer() =>
            new(id: _localUser.Id.Value, data: new Dictionary<string, PlayerDataObject>
            {
                [UgsLobby.PlayerNameKey] = new(PlayerDataObject.VisibilityOptions.Member, _localUser.DisplayName),
            });

        private DataObject PublicData(string key, string value) =>
            key == LobbyDataKeys.Endpoints ? new DataObject(DataObject.VisibilityOptions.Member, value)
            : _index.TryGetIndex(key, out var slot) ? new DataObject(DataObject.VisibilityOptions.Public, value, slot)
            : new DataObject(DataObject.VisibilityOptions.Public, value);

        private async UniTask RemoveLocalPlayerAsync(string lobbyId)
        {
            var removed = await UgsCalls.Run(Api.RemovePlayerAsync(lobbyId, _localUser.Id.Value), "Leave lobby", CancellationToken.None);
            if (!removed.IsSuccess && removed.Error.Code != OnlineErrorCode.NotFound) Debug.LogWarning($"[Online] {removed.Error}");
        }

        /// <summary>A create/join the caller gave up on: once it lands, undo it.</summary>
        private async UniTaskVoid AbandonWhenDone(System.Threading.Tasks.Task<Lobby> call, bool deleteIfOwner)
        {
            var outcome = await UgsCalls.Run(call, "Abandoned lobby request", CancellationToken.None);
            if (!outcome.IsSuccess) return;

            if (deleteIfOwner && outcome.Value.HostId == _localUser.Id.Value)
                await UgsCalls.Run(Api.DeleteLobbyAsync(outcome.Value.Id), "Delete abandoned lobby", CancellationToken.None);
            else
                await RemoveLocalPlayerAsync(outcome.Value.Id);
        }
    }
}
