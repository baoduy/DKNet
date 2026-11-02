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
/// A <c>params</c> array argument is spread into individual arguments, except a lone null element: spread
/// to a bare <c>null!</c> it would rebind to the whole array being null, so it keeps its array form.
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
