using DKNet.AspCore.Extensions.Endpoints;
using NetArchTest.Rules;

namespace AspCore.Extensions.Tests.Architecture;

/// <summary>
///     Mirrors <c>EfCore.Abstractions.Tests.Architecture.CrudActionDependencyBoundaryTests</c> from the other
///     side of the boundary (spec DRK-858 Appendix B.1): <c>MapActionById</c> takes its HTTP verb as a plain
///     string precisely so it never needs <see cref="CrudActionVerb" />. Scoped to the mapper's own type —
///     <c>DKNet.AspCore.Extensions</c> as a whole already has an unrelated, pre-existing dependency on
///     <c>DKNet.EfCore.Abstractions</c> (the entity-endpoint mappers), so a whole-assembly check would fail on
///     that unrelated surface rather than proving anything about the action mapper.
/// </summary>
public sealed class CrudActionDependencyBoundaryTests
{
    [Fact]
    public void FluentsEndpointMapperExtensions_HasNoDependencyOnEfCoreAbstractions()
    {
        var result = Types.InAssembly(typeof(CrudMapOptions).Assembly)
            .That()
            .HaveName(nameof(FluentsEndpointMapperExtensions))
            .Should()
            .NotHaveDependencyOn("DKNet.EfCore.Abstractions")
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "FluentsEndpointMapperExtensions (which declares MapActionById) must never reference " +
            "DKNet.EfCore.Abstractions — its httpMethod parameter stays a plain string rather than " +
            "CrudActionVerb specifically to avoid this dependency. New offenders: " + string.Join(", ", offenders));
    }
}
