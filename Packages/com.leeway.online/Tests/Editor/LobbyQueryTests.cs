using System.Collections.Generic;
using NUnit.Framework;

namespace Leeway.Online.Tests
{
    public class LobbyQueryTests
    {
        private static LobbySummary Lobby(int members, int max, params (string Key, string Value)[] data)
        {
            var dict = new Dictionary<string, string>();
            foreach (var (k, v) in data) dict[k] = v;
            return new LobbySummary(new LobbyId("1"), members, max, dict);
        }

        [Test]
        public void Builder_ProducesImmutableQueryWithAllConditions()
        {
            var query = LobbyQuery.Create()
                .WhereEquals("mode", "Coop")
                .WithProtocolVersion("1.0")
                .WithFreeSlots(2)
                .Limit(10)
                .Within(LobbySearchDistance.Worldwide)
                .Build();

            Assert.AreEqual(2, query.Filters.Count);
            Assert.AreEqual(2, query.MinFreeSlots);
            Assert.AreEqual(10, query.MaxResults);
            Assert.AreEqual(LobbySearchDistance.Worldwide, query.Distance);
        }

        [Test]
        public void Matches_ChecksDataAndFreeSlots()
        {
            var query = LobbyQuery.Create().WhereEquals("mode", "Coop").WithFreeSlots(1).Build();

            Assert.IsTrue(query.Matches(Lobby(1, 4, ("mode", "Coop"))));
            Assert.IsFalse(query.Matches(Lobby(1, 4, ("mode", "Versus"))), "wrong mode");
            Assert.IsFalse(query.Matches(Lobby(4, 4, ("mode", "Coop"))), "full");
            Assert.IsFalse(query.Matches(Lobby(1, 4)), "missing key");
        }

        [Test]
        public void NumericFilter_ComparesNumbersNotStrings()
        {
            var query = LobbyQuery.Create().WhereNumber("level", LobbyFilterComparison.GreaterThanOrEqual, 9).Build();

            Assert.IsTrue(query.Matches(Lobby(1, 4, ("level", "10"))), "\"10\" < \"9\" as strings, but 10 >= 9");
            Assert.IsFalse(query.Matches(Lobby(1, 4, ("level", "8"))));
            Assert.IsFalse(query.Matches(Lobby(1, 4, ("level", "x"))));
        }
    }
}
