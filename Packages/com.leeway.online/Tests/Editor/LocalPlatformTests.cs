using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Leeway.Online.Local;
using NUnit.Framework;

namespace Leeway.Online.Tests
{
    /// <summary>
    /// Two <see cref="LocalPlatform"/>s on one hub behave like two players on one lobby service —
    /// enough to pin the lobby contract every backend must honour.
    /// </summary>
    public class LocalPlatformTests
    {
        private LocalLobbyHub _hub;
        private LocalPlatform _host;
        private LocalPlatform _guest;

        [SetUp]
        public void SetUp()
        {
            _hub = new LocalLobbyHub();
            _host = new LocalPlatform(_hub, "Host");
            _guest = new LocalPlatform(_hub, "Guest");
            _host.InitializeAsync().GetAwaiter().GetResult();
            _guest.InitializeAsync().GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            _guest.Dispose();
            _host.Dispose();
        }

        private ILobby Create(int max = 4, LobbyVisibility visibility = LobbyVisibility.Public)
        {
            var result = _host.Lobbies.CreateAsync(new LobbyCreateOptions("Room", max, visibility).WithData("mode", "Coop")).GetAwaiter().GetResult();
            Assert.IsTrue(result.IsSuccess, result.ToString());
            return result.Value;
        }

        private OnlineResult<ILobby> Join(ILobby lobby) => _guest.Lobbies.JoinAsync(lobby.Id).GetAwaiter().GetResult();

        [Test]
        public void Create_MakesTheCreatorOwnerAndOnlyMember()
        {
            var lobby = Create();

            Assert.IsTrue(lobby.IsLocalUserOwner);
            Assert.AreEqual(1, lobby.Members.Count);
            Assert.AreEqual("Room", lobby.Name);
            Assert.AreEqual("Coop", lobby.GetData("mode"));
            Assert.AreSame(lobby, _host.Lobbies.Current);
        }

        [Test]
        public void Join_IsSeenByTheHostExactlyOnce()
        {
            var lobby = Create();
            var joined = new List<LobbyMember>();
            lobby.MemberJoined += joined.Add;

            var guestLobby = Join(lobby).Value;

            Assert.AreEqual(1, joined.Count);
            Assert.AreEqual("Guest", joined[0].DisplayName);
            Assert.AreEqual(2, guestLobby.Members.Count);
            Assert.IsFalse(guestLobby.IsLocalUserOwner);
        }

        [Test]
        public void Join_FailsWhenFull()
        {
            var lobby = Create(max: 1);
            var result = Join(lobby);
            Assert.AreEqual(OnlineErrorCode.LobbyFull, result.Error.Code);
            Assert.IsNull(_guest.Lobbies.Current);
        }

        [Test]
        public void Join_FailsForUnknownLobby()
        {
            var result = _guest.Lobbies.JoinAsync(new LobbyId("nope")).GetAwaiter().GetResult();
            Assert.AreEqual(OnlineErrorCode.NotFound, result.Error.Code);
        }

        [Test]
        public void OnlyTheOwnerMayWriteLobbyData()
        {
            var lobby = Create();
            var guestLobby = Join(lobby).Value;

            Assert.AreEqual(OnlineErrorCode.NotLobbyOwner, guestLobby.SetData("phase", "InGame").Error.Code);
            Assert.IsTrue(lobby.SetData("phase", "InGame").IsSuccess);
            Assert.AreEqual("InGame", guestLobby.GetData("phase"), "the guest sees the owner's write");
        }

        [Test]
        public void MemberData_IsVisibleToOthersAndRaisesAnEvent()
        {
            var lobby = Create();
            var guestLobby = Join(lobby).Value;
            int changes = 0;
            lobby.MemberDataChanged += _ => changes++;

            guestLobby.SetLocalMemberData("ready", "1");

            Assert.AreEqual(1, changes);
            Assert.AreEqual("1", lobby.GetMemberData(_guest.LocalUser.Id, "ready"));
        }

        [Test]
        public void OwnerLeaving_MigratesOwnershipToTheGuest()
        {
            var lobby = Create();
            var guestLobby = Join(lobby).Value;
            UserId? newOwner = null;
            guestLobby.OwnerChanged += id => newOwner = id;

            _host.Lobbies.Leave();

            Assert.AreEqual(_guest.LocalUser.Id, newOwner);
            Assert.IsTrue(guestLobby.IsLocalUserOwner);
            Assert.AreEqual(1, guestLobby.Members.Count);
        }

        [Test]
        public void Leave_DeactivatesTheOldLobbyObject()
        {
            var lobby = Create();
            LobbyId? left = null;
            _host.Lobbies.Left += (id, _) => left = id;

            _host.Lobbies.Leave();

            Assert.AreEqual(lobby.Id, left);
            Assert.IsNull(_host.Lobbies.Current);
            Assert.AreEqual(OnlineErrorCode.NotInLobby, lobby.SetData("x", "y").Error.Code);
        }

        [Test]
        public void Chat_ReachesEveryMember()
        {
            var lobby = Create();
            var guestLobby = Join(lobby).Value;
            var received = new List<LobbyChatMessage>();
            lobby.ChatMessageReceived += received.Add;

            guestLobby.SendChatMessage("  hello  ");

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("hello", received[0].Text);
            Assert.AreEqual("Guest", received[0].Sender.DisplayName);
        }

        [Test]
        public void Search_ListsOnlyPublicJoinableMatchingLobbies()
        {
            Create(visibility: LobbyVisibility.Public);
            var other = new LocalPlatform(_hub, "Other");
            other.InitializeAsync().GetAwaiter().GetResult();
            other.Lobbies.CreateAsync(new LobbyCreateOptions("Hidden", 4, LobbyVisibility.Private)).GetAwaiter().GetResult();

            var found = _guest.Lobbies.SearchAsync(LobbyQuery.Create().WhereEquals("mode", "Coop").Build()).GetAwaiter().GetResult();

            Assert.IsTrue(found.IsSuccess);
            Assert.AreEqual(1, found.Value.Count);
            Assert.AreEqual("Room", found.Value[0].Name);
            other.Dispose();
        }

        [Test]
        public void JoinRequest_RaisedBeforeAnyoneListens_IsReplayedToTheFirstSubscriber()
        {
            var lobby = Create();
            _guest.SimulateInvite(lobby.Id, _host.LocalUser.Id);

            LobbyJoinRequest? request = null;
            _guest.Lobbies.JoinRequested += r => request = r;

            Assert.AreEqual(lobby.Id, request?.LobbyId);
        }
    }
}
