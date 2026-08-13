using DKNet.AspCore.Idempotency;
using NetArchTest.Rules;
using Shouldly;

namespace AspCore.Idempotency.Tests.Architecture;

/// <summary>
///     Enforces that <c>DKNet.AspCore.Idempotency</c> — an ASP.NET Core endpoint-filter package — never depends on
///     <c>Microsoft.AspNetCore.Components</c> (Blazor). The two namespaces both publish a <c>RouteAttribute</c> with a
///     <c>Template</c> property, so a stray <c>using Microsoft.AspNetCore.Components;</c> compiles cleanly while
///     binding endpoint-metadata lookups to the Blazor attribute, which is never present on an ASP.NET Core endpoint.
///     The lookup then silently returns null forever and the code falls through to its fallback path.
///     <para>
///         This is a Tier-2 baseline rule (architecture-review DRK-320): there is exactly one known offender today,
///         <c>IdempotencyEndpointFilter</c> (<c>IdempotencyEndpointFilter.cs:5,139</c>), which is on the allow-list
///         below. The allow-list must only ever SHRINK — when the filter is migrated off the Blazor attribute, delete
///         its entry so the rule covers the whole assembly. Do not add new names to it.
///     </para>
/// </summary>
public sealed class BlazorDependencyArchitectureTests
{
    #region Fields

    private const string BlazorNamespace = "Microsoft.AspNetCore.Components";

    private const string OffenderTypeName = "IdempotencyEndpointFilter";

    /// <summary>
    ///     Today's known offenders. Must only shrink — never add a name here to silence a new violation.
    /// </summary>
    private static readonly string[] KnownViolations = [OffenderTypeName];

    #endregion

    #region Methods

    [Fact]
    public void IdempotencyTypes_ExceptKnownOffenders_MustNotDependOnBlazorComponents()
    {
        var result = Types.InAssembly(typeof(IdempotentKeyInfo).Assembly)
            .That()
            .DoNotHaveName(KnownViolations)
            .Should()
            .NotHaveDependencyOn(BlazorNamespace)
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "An ASP.NET Core idempotency filter must resolve route metadata through ASP.NET Core routing types, not " +
            "Blazor's Microsoft.AspNetCore.Components.RouteAttribute — the Blazor attribute is never attached to an " +
            "endpoint, so any GetMetadata<RouteAttribute>() call against it always returns null and the intended " +
            "behaviour silently never runs. New offenders: " + string.Join(", ", offenders) +
            ". Fix by using RouteEndpoint.RoutePattern.RawText (or IRouteDiagnosticsMetadata); do not add the type " +
            "to the KnownViolations allow-list.");
    }

    [Fact]
    public void Rule_CanDetectBlazorDependency_OnTheKnownOffender()
    {
        // Self-check: the allow-listed offender genuinely depends on Microsoft.AspNetCore.Components. If this ever
        // passes, the rule above has gone blind (NetArchTest can no longer see the dependency) and would silently
        // stop enforcing anything.
        var result = Types.InAssembly(typeof(IdempotentKeyInfo).Assembly)
            .That()
            .HaveName(OffenderTypeName)
            .Should()
            .NotHaveDependencyOn(BlazorNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeFalse(
            $"The known offender {OffenderTypeName} must still be detected as depending on {BlazorNamespace}; " +
            "if this assertion fails the enforcement rule can no longer see Blazor usage and is worthless.");
    }

    #endregion
}
