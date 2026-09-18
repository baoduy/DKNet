using System.Linq;
using System.Reflection;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;

namespace AspCore.Extensions.Tests.Architecture;

/// <summary>
///     DRK-1542 §5 — the declaration is the only public way to require a scope on a group, and the HTTP method
///     values it may name are fixed at build time (scenarios 7 and 8).
/// </summary>
public sealed class GroupScopePublicSurfaceTests
{
    [Fact]
    public void EndpointGroupScopeAttribute_AndEndpointHttpMethods_ArePublic_GroupScopeAuthorization_IsNot()
    {
        typeof(EndpointGroupScopeAttribute).IsPublic.ShouldBeTrue();
        typeof(EndpointHttpMethods).IsPublic.ShouldBeTrue();

        var mechanism = typeof(EndpointGroupScopeAttribute).Assembly.GetType(
            "DKNet.AspCore.Extensions.Endpoints.GroupScopeAuthorization", throwOnError: true)!;
        mechanism.IsPublic.ShouldBeFalse();
        mechanism.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ShouldBeEmpty();
    }

    [Fact]
    public void EndpointHttpMethods_EveryField_IsACompileTimeConstant()
    {
        var fields = typeof(EndpointHttpMethods).GetFields(BindingFlags.Public | BindingFlags.Static);

        fields.ShouldNotBeEmpty();
        foreach (var field in fields)
        {
            field.IsLiteral.ShouldBeTrue($"{field.Name} must be a compile-time constant (const), not just static");
            field.IsInitOnly.ShouldBeFalse($"{field.Name} must not be a readonly field");
        }
    }

    /// <summary>DRK-1548 §7 S4 — the group-wide policy member is gone.</summary>
    [Fact]
    public void IEndpointConfig_HasNoAuthPolicyMember()
    {
        var memberNames = typeof(IEndpointConfig)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        // Checked by string, not nameof(IEndpointConfig.AuthPolicy): this test stays compilable once the Build
        // stage removes the member it is proving the absence of.
        memberNames.ShouldNotContain("AuthPolicy");
        memberNames.ShouldNotContain("get_AuthPolicy");
    }
}
