using System.Security.Claims;
using DKNet.AspCore.Extensions;
using Microsoft.AspNetCore.Http;

namespace AspCore.Extensions.Tests;

/// <summary>
///     Unit-level coverage for <see cref="FromClaimAttribute" /> and the internal <c>ClaimValueResolver</c> that
///     resolves it (DRK-565) — exercised directly, without a full HTTP round trip, since both are plain,
///     side-effect-free pieces of logic.
/// </summary>
public class ContextualValueResolverTests
{
    #region Methods

    private static HttpContext CreateHttpContext(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        if (claims.Length > 0)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return context;
    }

    [Fact]
    public void FromClaimAttribute_Constructed_ExposesClaimTypeGivenToConstructor()
    {
        var attribute = new FromClaimAttribute(ClaimTypes.Name);

        attribute.ClaimType.ShouldBe(ClaimTypes.Name);
    }

    [Fact]
    public void FromClaimAttribute_NullClaimType_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new FromClaimAttribute(null!));

    [Fact]
    public void CanResolve_FromClaimAttributeSource_ReturnsTrue()
    {
        var resolver = new ClaimValueResolver();

        resolver.CanResolve(new FromClaimAttribute(ClaimTypes.Name)).ShouldBeTrue();
    }

    [Fact]
    public void CanResolve_NonFromClaimSource_ReturnsFalse()
    {
        var resolver = new ClaimValueResolver();

        resolver.CanResolve(new OtherContextualSource()).ShouldBeFalse();
    }

    [Fact]
    public void Resolve_ClaimPresentOnUser_ReturnsClaimValue()
    {
        var resolver = new ClaimValueResolver();
        var httpContext = CreateHttpContext(new Claim(ClaimTypes.Name, "alice"));

        var value = resolver.Resolve(new FromClaimAttribute(ClaimTypes.Name), httpContext);

        value.ShouldBe("alice");
    }

    [Fact]
    public void Resolve_ClaimAbsentFromUser_ReturnsNull()
    {
        var resolver = new ClaimValueResolver();
        var httpContext = CreateHttpContext(); // authenticated user, but no ClaimTypes.Name claim on it

        var value = resolver.Resolve(new FromClaimAttribute(ClaimTypes.Name), httpContext);

        value.ShouldBeNull();
    }

    /// <summary>A second, unrelated <see cref="IContextualSource" /> implementation — proves resolver dispatch is type-keyed.</summary>
    private sealed class OtherContextualSource : Attribute, IContextualSource;

    #endregion
}
