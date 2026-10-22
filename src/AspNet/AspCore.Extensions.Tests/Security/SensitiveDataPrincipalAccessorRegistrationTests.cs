using System.Security.Claims;
using DKNet.AspCore.Extensions.Security;
using DKNet.EfCore.Extensions.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Security;

/// <summary>
///     The DRK-2362 §6a D4 rows of <see cref="SensitiveDataPrincipalAccessorConfig.AddSensitiveDataPrincipalAccessor" />:
///     a second call adds nothing, and an accessor the app registered first is kept.
/// </summary>
public class SensitiveDataPrincipalAccessorRegistrationTests
{
    #region Methods

    [Fact]
    public void AddSensitiveDataPrincipalAccessor_CalledTwice_RegistersOneAccessor()
    {
        var services = new ServiceCollection();

        services.AddSensitiveDataPrincipalAccessor().AddSensitiveDataPrincipalAccessor();

        services.Count(d => d.ServiceType == typeof(ISensitiveDataPrincipalAccessor)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IHttpContextAccessor)).ShouldBe(1);
    }

    [Fact]
    public void AddSensitiveDataPrincipalAccessor_AppRegisteredItsOwn_KeepsAppAccessor()
    {
        var appAccessor = new AppAccessor();
        var services = new ServiceCollection();
        services.AddSingleton<ISensitiveDataPrincipalAccessor>(appAccessor);

        services.AddSensitiveDataPrincipalAccessor();
        using var provider = services.BuildServiceProvider();

        services.Count(d => d.ServiceType == typeof(ISensitiveDataPrincipalAccessor)).ShouldBe(1);
        provider.GetRequiredService<ISensitiveDataPrincipalAccessor>().ShouldBeSameAs(appAccessor);
    }

    [Fact]
    public void AddSensitiveDataPrincipalAccessor_RequestRunning_ReturnsUserOfRequest()
    {
        var services = new ServiceCollection();
        services.AddSensitiveDataPrincipalAccessor();
        using var provider = services.BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "finance")], "Test"));
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user };

        provider.GetRequiredService<ISensitiveDataPrincipalAccessor>().Current.ShouldBeSameAs(user);
    }

    #endregion

    private sealed class AppAccessor : ISensitiveDataPrincipalAccessor
    {
        public ClaimsPrincipal? Current => null;
    }
}
