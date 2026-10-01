using DKNet.Svc.PdfGenerators.Services;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     Acceptance tests for the DRK-1902 request classifier (brief DRK-1935 §3-4, rules R1-R3).
///     Every address case uses an IP literal so no test depends on DNS; the only DNS case uses the
///     RFC 6761 <c>.invalid</c> TLD, which never resolves.
/// </summary>
public class RequestGuardTests
{
    #region Methods

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://127.255.255.254:8080/admin")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://0.1.2.3/")]
    [InlineData("http://[::]/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://169.254.0.1/")]
    [InlineData("http://[fe80::1]/")]
    [InlineData("http://[febf::1]/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://10.255.255.255/")]
    [InlineData("http://172.16.0.0/")]
    [InlineData("http://172.31.255.255/")]
    [InlineData("http://192.168.0.1/")]
    [InlineData("http://192.168.255.255/")]
    [InlineData("http://100.64.0.0/")]
    [InlineData("http://100.127.255.255/")]
    [InlineData("http://[fc00::1]/")]
    [InlineData("http://[fdff:ffff::1]/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://[::ffff:169.254.169.254]/")]
    [InlineData("http://[::ffff:10.0.0.1]/")]
    [InlineData("http://[::ffff:192.168.1.1]/")]
    [InlineData("http://localhost:8080/")]
    public async Task IsAllowedAsync_BlockedRangeAddressWithoutOptIn_ReturnsFalse(string url)
    {
        // Act
        var allowed = await RequestGuard.IsAllowedAsync(url, false);

        // Assert
        allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://172.16.0.0/")]
    [InlineData("http://192.168.0.1/")]
    [InlineData("http://100.64.0.0/")]
    [InlineData("http://[fc00::1]/")]
    [InlineData("http://[fe80::1]/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://localhost:8080/")]
    public async Task IsAllowedAsync_BlockedRangeAddressWithOptIn_ReturnsTrue(string url)
    {
        // Act
        var allowed = await RequestGuard.IsAllowedAsync(url, true);

        // Assert
        allowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("http://8.8.8.8/")]
    [InlineData("https://1.1.1.1/style.css")]
    [InlineData("http://11.0.0.1/")]
    [InlineData("http://9.255.255.255/")]
    [InlineData("http://128.0.0.1/")]
    [InlineData("http://1.0.0.0/")]
    [InlineData("http://172.15.255.255/")]
    [InlineData("http://172.32.0.0/")]
    [InlineData("http://192.167.255.255/")]
    [InlineData("http://192.169.0.0/")]
    [InlineData("http://169.253.255.255/")]
    [InlineData("http://169.255.0.0/")]
    [InlineData("http://100.63.255.255/")]
    [InlineData("http://100.128.0.0/")]
    [InlineData("http://[2001:4860:4860::8888]/")]
    [InlineData("http://[fe7f::1]/")]
    [InlineData("http://[fbff::1]/")]
    [InlineData("http://[::ffff:8.8.8.8]/")]
    public async Task IsAllowedAsync_PublicAddressWithoutOptIn_ReturnsTrue(string url)
    {
        // Act
        var allowed = await RequestGuard.IsAllowedAsync(url, false);

        // Assert
        allowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("data:text/plain,hello", false)]
    [InlineData("data:image/png;base64,iVBORw0KGgo=", false)]
    [InlineData("data:text/plain,hello", true)]
    public async Task IsAllowedAsync_DataScheme_ReturnsTrue(string url, bool allowPrivateNetworkRequests)
    {
        // Act
        var allowed = await RequestGuard.IsAllowedAsync(url, allowPrivateNetworkRequests);

        // Assert
        allowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("file:///etc/passwd", true)]
    [InlineData("ftp://8.8.8.8/file.txt", true)]
    [InlineData("ws://8.8.8.8/socket", true)]
    [InlineData("wss://8.8.8.8/socket", true)]
    [InlineData("gopher://8.8.8.8/", true)]
    [InlineData("javascript:alert(1)", true)]
    [InlineData("chrome://settings/", true)]
    [InlineData("blob:https://8.8.8.8/0b8c1e4e-3f0a-4d4c-9e0f-2a1b3c4d5e6f", true)]
    [InlineData("about:blank", true)]
    public async Task IsAllowedAsync_NonWebScheme_ReturnsFalseWhateverTheOptIn(string url,
        bool allowPrivateNetworkRequests)
    {
        // Act
        var allowed = await RequestGuard.IsAllowedAsync(url, allowPrivateNetworkRequests);

        // Assert
        allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsAllowedAsync_HostThatDoesNotResolve_ReturnsFalse(bool allowPrivateNetworkRequests)
    {
        // Act
        var allowed = await RequestGuard.IsAllowedAsync("http://drk-1902-probe.invalid/", allowPrivateNetworkRequests);

        // Assert
        allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("not a url", true)]
    [InlineData("/relative/path.png", true)]
    public async Task IsAllowedAsync_UrlThatIsNotAbsolute_ReturnsFalse(string url, bool allowPrivateNetworkRequests)
    {
        // Act
        var allowed = await RequestGuard.IsAllowedAsync(url, allowPrivateNetworkRequests);

        // Assert
        allowed.ShouldBeFalse();
    }

    #endregion
}
