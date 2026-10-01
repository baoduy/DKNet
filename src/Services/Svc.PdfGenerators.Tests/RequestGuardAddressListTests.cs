using System.Net;
using DKNet.Svc.PdfGenerators.Services;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     DRK-1902 rules R2 and R3 for a host name that resolves to several addresses, or to none.
/// </summary>
public class RequestGuardAddressListTests
{
    #region Methods

    [Fact]
    public void AreAllowed_PublicAndBlockedAddressWithoutOptIn_ReturnsFalse()
    {
        // Arrange
        IPAddress[] addresses = [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1")];

        // Act
        var allowed = RequestGuard.AreAllowed(addresses, false);

        // Assert
        allowed.ShouldBeFalse();
    }

    [Fact]
    public void AreAllowed_PublicAndBlockedAddressWithOptIn_ReturnsTrue()
    {
        // Arrange
        IPAddress[] addresses = [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1")];

        // Act
        var allowed = RequestGuard.AreAllowed(addresses, true);

        // Assert
        allowed.ShouldBeTrue();
    }

    [Fact]
    public void AreAllowed_OnlyPublicAddressesWithoutOptIn_ReturnsTrue()
    {
        // Arrange
        IPAddress[] addresses = [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2001:4860:4860::8888")];

        // Act
        var allowed = RequestGuard.AreAllowed(addresses, false);

        // Assert
        allowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AreAllowed_NoAddress_ReturnsFalse(bool allowPrivateNetworkRequests)
    {
        // Act
        var allowed = RequestGuard.AreAllowed([], allowPrivateNetworkRequests);

        // Assert
        allowed.ShouldBeFalse();
    }

    #endregion
}
