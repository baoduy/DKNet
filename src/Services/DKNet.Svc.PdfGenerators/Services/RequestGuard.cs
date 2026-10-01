using System.Net;
using System.Net.Sockets;

namespace DKNet.Svc.PdfGenerators.Services;

/// <summary>
///     Decides whether a request issued while rendering content may leave the browser.
/// </summary>
internal static class RequestGuard
{
    #region Fields

    /// <summary>
    ///     IPv4 loopback, unspecified, link-local, RFC 1918 private and CGNAT ranges.
    /// </summary>
    private static readonly IPNetwork[] BlockedIPv4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16")
    ];

    #endregion

    #region Methods

    /// <summary>
    ///     Returns <see langword="true" /> when the request to <paramref name="url" /> may be sent.
    /// </summary>
    /// <param name="url">The request URL as reported by the browser.</param>
    /// <param name="allowPrivateNetworkRequests">Whether loopback, link-local and private hosts are allowed.</param>
    /// <param name="cancellationToken">Cancels host name resolution.</param>
    /// <returns><see langword="true" /> to continue the request; <see langword="false" /> to abort it.</returns>
    internal static async Task<bool> IsAllowedAsync(string url, bool allowPrivateNetworkRequests,
        CancellationToken cancellationToken = default)
    {
        // Checked before parsing: a data: URL carries its payload inline and can exceed what Uri accepts.
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        // ponytail: the address checked here is resolved separately from the one Chromium connects to, so a
        // DNS-rebinding host can answer public here and private to the browser. Closing that window needs the
        // browser to connect through a proxy that pins the resolved address.
        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal))
            addresses = [literal];
        else
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (SocketException)
            {
                return false;
            }

        return AreAllowed(addresses, allowPrivateNetworkRequests);
    }

    /// <summary>
    ///     Returns <see langword="true" /> when a host resolving to <paramref name="addresses" /> may be requested:
    ///     there is at least one address and, unless <paramref name="allowPrivateNetworkRequests" /> is set, none
    ///     of them is in a blocked range.
    /// </summary>
    /// <param name="addresses">Every address the host is, or resolves to.</param>
    /// <param name="allowPrivateNetworkRequests">Whether loopback, link-local and private hosts are allowed.</param>
    /// <returns><see langword="true" /> to continue the request; <see langword="false" /> to abort it.</returns>
    internal static bool AreAllowed(IReadOnlyCollection<IPAddress> addresses, bool allowPrivateNetworkRequests) =>
        addresses.Count > 0 && (allowPrivateNetworkRequests || !addresses.Any(IsBlocked));

    /// <summary>
    ///     Returns <see langword="true" /> when <paramref name="address" /> is loopback, unspecified, link-local,
    ///     private, CGNAT or unique-local, including the IPv4-mapped IPv6 form of any of these.
    /// </summary>
    private static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return BlockedIPv4Networks.Any(network => network.Contains(address));

        return IPAddress.IsLoopback(address)
               || address.Equals(IPAddress.IPv6Any)
               || address.IsIPv6LinkLocal
               || address.IsIPv6UniqueLocal;
    }

    #endregion
}
