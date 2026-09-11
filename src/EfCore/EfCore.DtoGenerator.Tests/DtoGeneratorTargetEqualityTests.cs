// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     Direct reflection-based tests for <c>DtoGenerator.Target</c>'s hand-written <c>Equals(Target?)</c> /
///     <c>GetHashCode()</c> pair (DRK-1203 round-2 review finding): this record is the incremental
///     generator's cache key, is not record-synthesised (string-based comparison is deliberate — see the
///     comment beside it — because <see cref="SymbolEqualityComparer"/> is unstable across incremental
///     compilations), and dropping <c>[ExcludeFromCodeCoverage]</c> left it entirely unmeasured. <c>Target</c>
///     itself is a private nested type, so its public members are reached via reflection rather than a
///     direct reference.
/// </summary>
public class DtoGeneratorTargetEqualityTests
{
    #region Constants

    private static readonly Type TargetType =
        typeof(DKNet.EfCore.DtoGenerator.DtoGenerator).GetNestedType("Target", BindingFlags.NonPublic)!;

    private static readonly MethodInfo EqualsMethod =
        TargetType.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance, null, [TargetType], null)!;

    #endregion

    #region Methods

    [Fact]
    public void Equals_SameInstance_ReturnsTrue()
    {
        var target = CreateTarget(GetSymbol("Foo"), GetSymbol("Bar"));

        InvokeEquals(target, target).ShouldBeTrue();
    }

    [Fact]
    public void Equals_NullOther_ReturnsFalse()
    {
        var target = CreateTarget(GetSymbol("Foo"), GetSymbol("Bar"));

        InvokeEquals(target, null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_IdenticalContent_ReturnsTrue()
    {
        var dto = GetSymbol("Foo");
        var entity = GetSymbol("Bar");
        var a = CreateTarget(dto, entity, ["X"], ["Y"], true);
        var b = CreateTarget(dto, entity, ["X"], ["Y"], true);

        InvokeEquals(a, b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentDtoSymbol_ReturnsFalse()
    {
        var entity = GetSymbol("Bar");
        var a = CreateTarget(GetSymbol("Foo"), entity);
        var b = CreateTarget(GetSymbol("Baz"), entity);

        InvokeEquals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentEntitySymbol_ReturnsFalse()
    {
        var dto = GetSymbol("Foo");
        var a = CreateTarget(dto, GetSymbol("Bar"));
        var b = CreateTarget(dto, GetSymbol("Baz"));

        InvokeEquals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentExcludedPropertiesCount_ReturnsFalse()
    {
        var dto = GetSymbol("Foo");
        var entity = GetSymbol("Bar");
        var a = CreateTarget(dto, entity, excluded: ["X"]);
        var b = CreateTarget(dto, entity, excluded: ["X", "Y"]);

        InvokeEquals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameExcludedCountDifferentContent_ReturnsFalse()
    {
        var dto = GetSymbol("Foo");
        var entity = GetSymbol("Bar");
        var a = CreateTarget(dto, entity, excluded: ["X"]);
        var b = CreateTarget(dto, entity, excluded: ["Z"]);

        InvokeEquals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentIncludedPropertiesCount_ReturnsFalse()
    {
        var dto = GetSymbol("Foo");
        var entity = GetSymbol("Bar");
        var a = CreateTarget(dto, entity, included: ["X"]);
        var b = CreateTarget(dto, entity, included: ["X", "Y"]);

        InvokeEquals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameIncludedCountDifferentContent_ReturnsFalse()
    {
        var dto = GetSymbol("Foo");
        var entity = GetSymbol("Bar");
        var a = CreateTarget(dto, entity, included: ["X"]);
        var b = CreateTarget(dto, entity, included: ["Z"]);

        InvokeEquals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentIgnoreComplexType_ReturnsFalse()
    {
        var dto = GetSymbol("Foo");
        var entity = GetSymbol("Bar");
        var a = CreateTarget(dto, entity, ignoreComplexType: true);
        var b = CreateTarget(dto, entity, ignoreComplexType: false);

        InvokeEquals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_IdenticalContent_ProducesSameHash()
    {
        var dto = GetSymbol("Foo");
        var entity = GetSymbol("Bar");
        var a = CreateTarget(dto, entity, ["X", "Y"], ["Z"], true);
        var b = CreateTarget(dto, entity, ["X", "Y"], ["Z"], true);

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentContent_ProducesDifferentHash()
    {
        var a = CreateTarget(GetSymbol("Foo"), GetSymbol("Bar"), ["X"], [], false);
        var b = CreateTarget(GetSymbol("Baz"), GetSymbol("Bar"), ["X"], [], false);

        a.GetHashCode().ShouldNotBe(b.GetHashCode());
    }

    #endregion

    #region Internals

    private static bool InvokeEquals(object target, object? other) =>
        (bool)EqualsMethod.Invoke(target, [other])!;

    private static object CreateTarget(
        INamedTypeSymbol dtoSymbol,
        INamedTypeSymbol entitySymbol,
        IEnumerable<string>? excluded = null,
        IEnumerable<string>? included = null,
        bool? ignoreComplexType = null)
    {
        var target = Activator.CreateInstance(TargetType)!;
        TargetType.GetProperty("DtoSymbol")!.SetValue(target, dtoSymbol);
        TargetType.GetProperty("EntitySymbol")!.SetValue(target, entitySymbol);
        TargetType.GetProperty("ExcludedProperties")!.SetValue(target, new HashSet<string>(excluded ?? []));
        TargetType.GetProperty("IncludedProperties")!.SetValue(target, new HashSet<string>(included ?? []));
        TargetType.GetProperty("IgnoreComplexType")!.SetValue(target, ignoreComplexType);
        return target;
    }

    private static INamedTypeSymbol GetSymbol(string typeName)
    {
        const string source = """
            namespace Probe
            {
                public class Foo { }
                public class Bar { }
                public class Baz { }
            }
            """;
        var compilation = CSharpCompilation.Create(
            "TargetEqualityProbe",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return compilation.GetTypeByMetadataName($"Probe.{typeName}")!;
    }

    #endregion
}
