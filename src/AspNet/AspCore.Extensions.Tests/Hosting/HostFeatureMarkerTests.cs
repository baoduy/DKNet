// DRK-2362 per-host feature marker (§3 "Per-host feature marker"). HostFeatureMarker lands complete with the
// acceptance tests — it is the seam every stage-2 Build registers through — so these tests are green by design.

using DKNet.AspCore.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Hosting;

public class HostFeatureMarkerTests
{
    #region Methods

    [Fact]
    public void IsFeatureAdded_FeatureMarkedOnHost_ReturnsTrue()
    {
        var services = new ServiceCollection();

        services.MarkFeatureAdded("RateLimitConfig");

        using var provider = services.BuildServiceProvider();
        provider.IsFeatureAdded("RateLimitConfig").ShouldBeTrue();
    }

    [Fact]
    public void IsFeatureAdded_FeatureNotMarked_ReturnsFalse()
    {
        var services = new ServiceCollection();
        services.MarkFeatureAdded("RateLimitConfig");

        using var provider = services.BuildServiceProvider();

        provider.IsFeatureAdded("CorsConfig").ShouldBeFalse();
    }

    [Fact]
    public void IsFeatureAdded_FeatureMarkedOnAnotherHost_ReturnsFalse()
    {
        var ordersApi = new ServiceCollection();
        var billingApi = new ServiceCollection();

        ordersApi.MarkFeatureAdded("RateLimitConfig");

        using var ordersProvider = ordersApi.BuildServiceProvider();
        using var billingProvider = billingApi.BuildServiceProvider();
        ordersProvider.IsFeatureAdded("RateLimitConfig").ShouldBeTrue();
        billingProvider.IsFeatureAdded("RateLimitConfig").ShouldBeFalse();
    }

    [Fact]
    public void MarkFeatureAdded_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        services.MarkFeatureAdded("HttpsConfig").ShouldBeSameAs(services);
    }

    #endregion
}
