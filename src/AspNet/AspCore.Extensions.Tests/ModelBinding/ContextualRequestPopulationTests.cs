using System.Security.Claims;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.ModelBinding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.ModelBinding;

/// <summary>
///     Unit-level coverage for <c>ContextualMemberScanner</c>, the internal <c>ContextualRequestPopulationService</c>,
///     <see cref="ContextualPopulationOptions" />, and <see cref="ContextualRequestPopulationServiceCollectionExtensions" />
///     (DRK-565) — exercised directly against a real <see cref="HttpContext" />/<see cref="ClaimsPrincipal" />
///     rather than through a full HTTP round trip, so every branch (fallback on/off, conversion success/failure,
///     no-declared-members early return) is provable without standing up a host per case.
///     End-to-end HTTP-level proof that the mechanism is actually wired into request dispatch lives in
///     <see cref="ContextualRequestPopulationEndToEndTests" />.
/// </summary>
public class ContextualRequestPopulationTests
{
    #region Methods

    private static HttpContext CreateHttpContext(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        if (claims.Length > 0)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return context;
    }

    private static ContextualRequestPopulationService CreateService(ContextualPopulationOptions? options = null) =>
        new([new ClaimValueResolver()], options ?? new ContextualPopulationOptions());

    // --- ContextualMemberScanner -------------------------------------------------------------------------------

    [Fact]
    public void GetDeclaredMembers_TypeWithFromClaimProperty_ReturnsThatPropertyAndItsSource()
    {
        var members = ContextualMemberScanner.GetDeclaredMembers(typeof(ByUserProbeCommand));

        members.Length.ShouldBe(1);
        members[0].Property.Name.ShouldBe(nameof(ByUserProbeCommand.ByUser));
        members[0].Source.ShouldBeOfType<FromClaimAttribute>().ClaimType.ShouldBe(ClaimTypes.Name);
    }

    [Fact]
    public void GetDeclaredMembers_TypeWithNoDeclaredMembers_ReturnsEmptyArray() =>
        ContextualMemberScanner.GetDeclaredMembers(typeof(ValidatedCommand)).ShouldBeEmpty();

    [Fact]
    public void GetDeclaredMembers_TypeWithMultipleDeclaredMembers_ReturnsBoth() =>
        ContextualMemberScanner.GetDeclaredMembers(typeof(MultiClaimCommand)).Length.ShouldBe(2);

    [Fact]
    public void GetDeclaredMembers_PropertyHasNoSetter_ThrowsInvalidOperationExceptionNamingTypeAndMember()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => ContextualMemberScanner.GetDeclaredMembers(typeof(NoSetterFixture)));

        exception.Message.ShouldContain(nameof(NoSetterFixture));
        exception.Message.ShouldContain(nameof(NoSetterFixture.ByUser));
    }

    /// <summary>
    ///     A getter-only declared member — kept private to THIS file rather than the shared fixtures file, so it
    ///     is never scanned by <c>UseEndpointConfigs()</c>'s AppDomain-wide <c>IEndpointConfig</c> discovery (it
    ///     isn't one) and can never break any other test in the shared assembly (DRK-565 §5 item 11).
    /// </summary>
    private sealed record NoSetterFixture
    {
        [FromClaim(ClaimTypes.Name)]
        public string? ByUser { get; }
    }

    // --- ContextualRequestPopulationService.Populate -------------------------------------------------------

    [Fact]
    public void Populate_TypeWithNoDeclaredMembers_LeavesRequestUnchanged()
    {
        var service = CreateService();
        var request = new ValidatedCommand { Name = "untouched" };

        service.Populate(request, CreateHttpContext(), requireAuthorization: true);

        request.Name.ShouldBe("untouched");
    }

    [Fact]
    public void Populate_ClaimPresent_OverwritesCallerSuppliedValueWithClaimValue()
    {
        var service = CreateService();
        var request = new ByUserProbeCommand { ByUser = "forged-by-caller" };

        service.Populate(request, CreateHttpContext(new Claim(ClaimTypes.Name, "alice")), requireAuthorization: true);

        request.ByUser.ShouldBe("alice");
    }

    [Fact]
    public void Populate_ClaimMissing_RequireAuthorizationTrue_SetsTypeDefaultRegardlessOfFallback()
    {
        var service = CreateService(new ContextualPopulationOptions { SystemAccountFallback = "system-account" });
        var request = new ByUserProbeCommand { ByUser = "forged-by-caller" };

        service.Populate(request, CreateHttpContext(), requireAuthorization: true);

        // requireAuthorization: true -> the fallback must never leak across the auth-required boundary.
        request.ByUser.ShouldBeNull();
    }

    [Fact]
    public void Populate_ClaimMissing_RequireAuthorizationFalse_FallbackConfigured_UsesFallbackValue()
    {
        var service = CreateService(new ContextualPopulationOptions { SystemAccountFallback = "system-account" });
        var request = new ByUserProbeCommand();

        service.Populate(request, CreateHttpContext(), requireAuthorization: false);

        request.ByUser.ShouldBe("system-account");
    }

    [Fact]
    public void Populate_ClaimMissing_RequireAuthorizationFalse_NoFallbackConfigured_SetsTypeDefault()
    {
        var service = CreateService(); // SystemAccountFallback left null (default)
        var request = new ByUserProbeCommand();

        service.Populate(request, CreateHttpContext(), requireAuthorization: false);

        request.ByUser.ShouldBeNull();
    }

    [Fact]
    public void Populate_ClaimValueDoesNotConvertToPropertyType_SetsTypeDefault()
    {
        var service = CreateService();
        var request = new GuidClaimCommand { TenantId = Guid.NewGuid() };

        service.Populate(
            request, CreateHttpContext(new Claim("tenant-id", "not-a-guid")), requireAuthorization: true);

        request.TenantId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Populate_ClaimValueConvertsToPropertyType_SetsConvertedValue()
    {
        var service = CreateService();
        var request = new GuidClaimCommand();
        var tenantId = Guid.NewGuid();

        service.Populate(
            request,
            CreateHttpContext(new Claim("tenant-id", tenantId.ToString())),
            requireAuthorization: true);

        request.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Populate_MultipleDeclaredMembers_EachPopulatedFromItsOwnClaim()
    {
        var service = CreateService();
        var request = new MultiClaimCommand();

        service.Populate(
            request,
            CreateHttpContext(new Claim(ClaimTypes.Name, "alice"), new Claim("tenant-id", "acme")),
            requireAuthorization: true);

        request.ByUser.ShouldBe("alice");
        request.TenantId.ShouldBe("acme");
    }

    // --- ContextualPopulationOptions ---------------------------------------------------------------------------

    [Fact]
    public void ContextualPopulationOptions_Defaults_SystemAccountFallbackIsNull() =>
        new ContextualPopulationOptions().SystemAccountFallback.ShouldBeNull();

    [Fact]
    public void ContextualPopulationOptions_SystemAccountFallbackSet_ExposesConfiguredValue() =>
        new ContextualPopulationOptions { SystemAccountFallback = "system" }.SystemAccountFallback.ShouldBe("system");

    // --- AddContextualRequestPopulation (DI registration) ------------------------------------------------------

    [Fact]
    public void AddContextualRequestPopulation_NoConfigure_RegistersOptionsWithNullFallback()
    {
        var services = new ServiceCollection();

        services.AddContextualRequestPopulation();
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ContextualPopulationOptions>().SystemAccountFallback.ShouldBeNull();
    }

    [Fact]
    public void AddContextualRequestPopulation_WithConfigure_AppliesCallbackToRegisteredOptions()
    {
        var services = new ServiceCollection();

        services.AddContextualRequestPopulation(o => o.SystemAccountFallback = "system-account");
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ContextualPopulationOptions>().SystemAccountFallback.ShouldBe("system-account");
    }

    [Fact]
    public void AddContextualRequestPopulation_RegistersAClaimValueResolver()
    {
        var services = new ServiceCollection();

        services.AddContextualRequestPopulation();
        var provider = services.BuildServiceProvider();

        provider.GetServices<IContextualValueResolver>().ShouldContain(r => r is ClaimValueResolver);
    }

    // Note: OpenApiOptions exposes no public surface to inspect its registered transformer list, so proving
    // ContextualSourceSchemaTransformer/ContextualSourceOperationTransformer are actually wired is done end-to-end
    // against a real generated document in ContextualSourceOpenApiTests, rather than via reflection here.

    [Fact]
    public void AddContextualRequestPopulation_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        services.AddContextualRequestPopulation().ShouldBeSameAs(services);
    }

    #endregion
}
