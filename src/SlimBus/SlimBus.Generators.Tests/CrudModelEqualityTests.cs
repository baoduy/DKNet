using System.Collections.Immutable;
using DKNet.SlimBus.Generators;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
/// The CRUD model records feed Roslyn's incremental-generator cache: two models built from separate
/// runs over identical source must compare equal (cache hit, no re-emit). Default record equality
/// cannot provide that — <see cref="ImmutableArray{T}"/> compares by reference — so each record carries
/// hand-written <c>Equals</c>/<c>GetHashCode</c>. These tests pin that contract with separately-built
/// arrays, which is exactly the case reference equality gets wrong.
/// </summary>
public class CrudModelEqualityTests
{
    private static CrudParamModel BuildParam(params string[] annotations) =>
        new("name", "Name", "global::System.String", [.. annotations]);

    private static CrudMemberModel BuildCreate(bool handWritten = false) =>
        new("CreateProductRequest", ".ctor", true, [BuildParam("[Required]")], handWritten);

    private static CrudMemberModel BuildUpdate() =>
        new("UpdatePriceProductRequest", "UpdatePrice", false, [BuildParam()]);

    private static CrudEntityModel BuildEntity(CrudMemberModel? create) =>
        new("global::MyDomain.Product", "Product", "global::System.Guid",
            "global::MyApi.ProductDto", "ProductDto", create, [BuildUpdate()]);

    private static CrudGenerationResult BuildResult(Diagnostic diagnostic, string ns = "MyApi", bool emitEndpoints = true) =>
        new([BuildEntity(BuildCreate())], [diagnostic], ns, emitEndpoints);

    [Fact]
    public void Equals_SeparatelyBuiltIdenticalParamModels_AreEqualWithSameHashCode()
    {
        var first = BuildParam("[Required]");
        var second = BuildParam("[Required]");

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_ParamModelsDifferingOnlyInAnnotations_AreNotEqual()
    {
        BuildParam("[Required]").Equals(BuildParam("[Required]", "[StringLength(10)]")).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ParamModelComparedToNull_IsFalse()
    {
        BuildParam().Equals(null as CrudParamModel).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SeparatelyBuiltIdenticalMemberModels_AreEqualWithSameHashCode()
    {
        var first = BuildCreate();
        var second = BuildCreate();

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_MemberModelsDifferingOnlyInHandWrittenFlag_AreNotEqual()
    {
        // The flag decides whether the generated handler is skipped; a cache hit across it would emit stale output.
        BuildCreate(handWritten: false).Equals(BuildCreate(handWritten: true)).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SeparatelyBuiltIdenticalEntityModels_AreEqualWithSameHashCode()
    {
        var first = BuildEntity(BuildCreate());
        var second = BuildEntity(BuildCreate());

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_EntityModelsWithNullCreateOnBoth_AreEqualWithSameHashCode()
    {
        var first = BuildEntity(null);
        var second = BuildEntity(null);

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_EntityModelsDifferingOnlyInCreate_AreNotEqual()
    {
        BuildEntity(BuildCreate()).Equals(BuildEntity(null)).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SeparatelyBuiltIdenticalResults_AreEqualWithSameHashCode()
    {
        var diagnostic = Diagnostic.Create(CrudDiagnostics.NoDtoFound, Location.None, "Product");
        var first = BuildResult(diagnostic);
        var second = BuildResult(diagnostic);

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_ResultsDifferingOnlyInNamespace_AreNotEqual()
    {
        var diagnostic = Diagnostic.Create(CrudDiagnostics.NoDtoFound, Location.None, "Product");

        BuildResult(diagnostic, ns: "MyApi").Equals(BuildResult(diagnostic, ns: "OtherApi")).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ResultsDifferingOnlyInEmitEndpoints_AreNotEqual()
    {
        var diagnostic = Diagnostic.Create(CrudDiagnostics.NoDtoFound, Location.None, "Product");

        BuildResult(diagnostic, emitEndpoints: true).Equals(BuildResult(diagnostic, emitEndpoints: false)).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ResultComparedToNull_IsFalse()
    {
        var diagnostic = Diagnostic.Create(CrudDiagnostics.NoDtoFound, Location.None, "Product");

        BuildResult(diagnostic).Equals(null as CrudGenerationResult).ShouldBeFalse();
    }
}
