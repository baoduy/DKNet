using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
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
///         This is a Tier-2 baseline rule (architecture-review DRK-320). The former known offender,
///         <c>IdempotencyEndpointFilter</c>, was migrated off the Blazor attribute onto
///         <c>RouteEndpoint.RoutePattern.RawText</c> / <c>IRouteDiagnosticsMetadata</c> — the rule now covers the
///         whole assembly with no allow-list.
///     </para>
/// </summary>
public sealed class BlazorDependencyArchitectureTests
{
    #region Fields

    private const string BlazorNamespace = "Microsoft.AspNetCore.Components";

    #endregion

    #region Methods

    [Fact]
    public void IdempotencyTypes_ExceptKnownOffenders_MustNotDependOnBlazorComponents()
    {
        var result = Types.InAssembly(typeof(IdempotentKeyInfo).Assembly)
            .Should()
            .NotHaveDependencyOn(BlazorNamespace)
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "An ASP.NET Core idempotency filter must resolve route metadata through ASP.NET Core routing types, not " +
            "Blazor's Microsoft.AspNetCore.Components.RouteAttribute — the Blazor attribute is never attached to an " +
            "endpoint, so any GetMetadata<RouteAttribute>() call against it always returns null and the intended " +
            "behaviour silently never runs. New offenders: " + string.Join(", ", offenders) +
            ". Fix by using RouteEndpoint.RoutePattern.RawText (or IRouteDiagnosticsMetadata).");
    }

    #endregion
}
