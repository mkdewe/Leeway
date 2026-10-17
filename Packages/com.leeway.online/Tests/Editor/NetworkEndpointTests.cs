using NUnit.Framework;

namespace Leeway.Online.Tests
{
    public class NetworkEndpointTests
    {
        [Test]
        public void Serialize_ThenParse_RoundTrips()
        {
            var endpoint = new NetworkEndpoint("UDP", "192.168.1.20", 7770);
            Assert.IsTrue(NetworkEndpoint.TryParse(endpoint.Serialize(), out var parsed));
            Assert.AreEqual(endpoint, parsed);
            Assert.AreEqual("udp", parsed.Scheme, "Schemes are normalised to lower case.");
        }

        [Test]
        public void Ipv6Address_SurvivesBecauseTheSeparatorIsNotAColon()
        {
            var endpoint = new NetworkEndpoint("udp", "fe80::1", 7770);
            Assert.IsTrue(NetworkEndpoint.TryParse(endpoint.Serialize(), out var parsed));
            Assert.AreEqual("fe80::1", parsed.Address);
        }

        [TestCase("")]
        [TestCase("udp|1.2.3.4")]
        [TestCase("udp|1.2.3.4|notaport")]
        [TestCase("|1.2.3.4|7770")]
        public void TryParse_RejectsMalformedInput(string text) =>
            Assert.IsFalse(NetworkEndpoint.TryParse(text, out _));

        [Test]
        public void EndpointList_KeepsOrderAndSkipsGarbage()
        {
            var text = NetworkEndpointList.Serialize(new[]
            {
                new NetworkEndpoint("steam", "76561198000000000"),
                new NetworkEndpoint("udp", "10.0.0.2", 7770),
            }) + ";garbage";

            var parsed = NetworkEndpointList.Parse(text);
            Assert.AreEqual(2, parsed.Count);
            Assert.AreEqual("steam", parsed[0].Scheme);
            Assert.AreEqual("udp", parsed[1].Scheme);
        }
    }
}
