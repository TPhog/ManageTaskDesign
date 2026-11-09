using System.Net;
using FluentAssertions;
using FreelanceFlow.Api.Features.Intake;

namespace FreelanceFlow.UnitTests.Features;

public class ClientIpKeyTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]                                  // IPv4 unchanged
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]                           // IPv4-mapped = the same client
    [InlineData("2001:db8:abcd:12:1:2:3:4", "2001:db8:abcd:12::/64")]          // one /64 = one subscriber
    [InlineData("2001:db8:abcd:12:ffff:ffff:ffff:ffff", "2001:db8:abcd:12::/64")]
    [InlineData("2001:db8:abcd:13::1", "2001:db8:abcd:13::/64")]
    public void Ipv6_clients_are_keyed_by_their_64_prefix(string address, string key) =>
        ClientIpKey.For(IPAddress.Parse(address)).Should().Be(key);

    [Fact]
    public void Unknown_address_shares_one_key() => ClientIpKey.For(null).Should().Be("unknown");
}
