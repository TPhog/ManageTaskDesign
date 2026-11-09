using System.Net;
using System.Net.Sockets;

namespace FreelanceFlow.Api.Features.Intake;

/// <summary>
/// Partition key of the public-intake per-IP limits. IPv4 as is; IPv6 by its /64 prefix — one subscriber usually owns a
/// whole /64 and can rotate the low 64 bits freely, so keying on the full address would void the limits.
/// </summary>
public static class ClientIpKey
{
    public static string For(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
