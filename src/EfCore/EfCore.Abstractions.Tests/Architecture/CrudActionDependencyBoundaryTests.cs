using NetArchTest.Rules;

namespace EfCore.Abstractions.Tests.Architecture;

/// <summary>
///     Enforces the two dependency-boundary invariants the domain-action annotation must never break (spec
///     DRK-858 Appendix B.1, no Gherkin scenario of its own): a domain author who only takes
///     <c>DKNet.EfCore.Abstractions</c> (to declare <c>[CrudAction]</c> and choose its <see cref="CrudActionVerb" />)
///     must never pull in ASP.NET Core — endpoint registration stays opt-in on the
///     <c>DKNet.AspCore.Extensions</c> package reference, not on the annotation itself.
/// </summary>
public sealed class CrudActionDependencyBoundaryTests
{
    [Fact]
    public void AbstractionsAssembly_HasNoDependencyOnAspNetCore()
    {
        var result = Types.InAssembly(typeof(CrudActionAttribute).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "DKNet.EfCore.Abstractions (the domain layer) must never depend on ASP.NET Core through the " +
            "[CrudAction] annotation or its CrudActionVerb option, or endpoint registration stops being opt-in. " +
            "New offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AbstractionsAssembly_HasNoDependencyOnAspCoreExtensions()
    {
        var result = Types.InAssembly(typeof(CrudActionAttribute).Assembly)
            .Should()
            .NotHaveDependencyOn("DKNet.AspCore.Extensions")
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "DKNet.EfCore.Abstractions must never reference DKNet.AspCore.Extensions — the by-id action mapper " +
            "takes its HTTP verb as a plain string precisely so this dependency never has to exist. " +
            "New offenders: " + string.Join(", ", offenders));
    }
}
