namespace DKNet.Svc.PdfGenerators.Services;

/// <summary>
///     Decides whether a request issued while rendering content may leave the browser.
/// </summary>
internal static class RequestGuard
{
    #region Methods

    /// <summary>
    ///     Returns <see langword="true" /> when the request to <paramref name="url" /> may be sent.
    /// </summary>
    /// <param name="url">The request URL as reported by the browser.</param>
    /// <param name="allowPrivateNetworkRequests">Whether loopback, link-local and private hosts are allowed.</param>
    /// <param name="cancellationToken">Cancels host name resolution.</param>
    /// <returns><see langword="true" /> to continue the request; <see langword="false" /> to abort it.</returns>
    internal static Task<bool> IsAllowedAsync(string url, bool allowPrivateNetworkRequests,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    #endregion
}
