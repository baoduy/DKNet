using System;
using System.IO;
using System.Linq;
using DKNet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
/// Formatted attribute text must re-parse to the same constructor and argument types. A <c>params</c> array
/// argument is spread into individual arguments, except a lone null element: spread to a bare <c>null!</c>
/// it would rebind to the whole array being null, so it keeps its array form. Numeric constants keep
/// their own type through a <c>.0</c>, a suffix or a cast.
/// </summary>
public class AttributeSourceFormatterTests
{
    [Theory]
    [InlineData("[AllowedValues((object)null)]")]
    [InlineData("[AllowedValues(new object[] { null })]")]
    public void Format_WithLoneNullParamsElement_KeepsTypedArrayThatRoundTripsToOneElement(string usage)
    {
        var text = FormatFirstAttribute(usage);

        text.ShouldBe("[AllowedValues(new object[] { null! })]");
        var roundTripped = FirstAttribute(text).ConstructorArguments.Single();
        roundTripped.IsNull.ShouldBeFalse();
        roundTripped.Values.Length.ShouldBe(1);
    }

    [Fact]
    public void Format_WithNullAmongSeveralParamsElements_StillSpreadsThem() =>
        FormatFirstAttribute("[AllowedValues(1, null)]").ShouldBe("[AllowedValues(1, null!)]");

    [Theory]
    [InlineData("[Range(0.0, 100.0)]", "[Range(0.0, 100.0)]")]
    [InlineData("[AllowedValues(1.0, 2.5)]", "[AllowedValues(1.0, 2.5)]")]
    [InlineData("[AllowedValues(-1.0, 1E+20)]", "[AllowedValues(-1.0, 1E+20)]")]
    [InlineData("[AllowedValues(1L, 2L)]", "[AllowedValues(1L, 2L)]")]
    [InlineData("[AllowedValues(1U, 2UL)]", "[AllowedValues(1U, 2UL)]")]
    [InlineData("[AllowedValues((short)1, (byte)2)]", "[AllowedValues(((short)1), ((byte)2))]")]
    [InlineData("[AllowedValues((sbyte)-1, (ushort)3)]", "[AllowedValues(((sbyte)-1), ((ushort)3))]")]
    public void Format_WithNumericArguments_RoundTripsToSameConstructorAndArgumentTypes(string usage, string expected)
    {
        var original = FirstAttribute(usage);
        var name = original.AttributeClass!.Name;

        var text = AttributeSourceFormatter.Format(name.Substring(0, name.Length - "Attribute".Length), original);

        text.ShouldBe(expected);
        var roundTripped = FirstAttribute(text);
        roundTripped.AttributeConstructor!.ToDisplayString().ShouldBe(original.AttributeConstructor!.ToDisplayString());
        TypedArguments(roundTripped).ShouldBe(TypedArguments(original));
    }

    // Each constructor argument (array elements flattened) as its type and boxed value.
    private static (string Type, object? Value)[] TypedArguments(AttributeData attribute) =>
        attribute.ConstructorArguments
            .SelectMany(arg => arg.Kind == TypedConstantKind.Array ? arg.Values : [arg])
            .Select(arg => (arg.Type!.ToDisplayString(), arg.Value))
            .ToArray();

    private static string FormatFirstAttribute(string usage) =>
        AttributeSourceFormatter.Format("AllowedValues", FirstAttribute(usage));

    private static AttributeData FirstAttribute(string usage)
    {
        var source = $"using System.ComponentModel.DataAnnotations; class Probe {{ {usage} public int Value; }}";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Probe", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation.GetTypeByMetadataName("Probe")!.GetMembers("Value").Single().GetAttributes().Single();
    }
}
