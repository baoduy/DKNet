// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

/// <summary>
///     In-process generator-driver tests pinning <c>DtoGenerator</c>'s attribute-argument emission
///     (DRK-1203 §3 rows 3-4, review follow-ups to PR #432): a null-valued constructor argument must not
///     emit a bare <c>null</c> literal into a <c>#nullable enable</c> generated file, and the spread form
///     used for <c>params</c> array arguments must never apply to a plain (non-<c>params</c>) array
///     parameter. Both probe attributes live in <c>System.ComponentModel.DataAnnotations</c> so
///     <see cref="DKNet.EfCore.DtoGenerator.DtoGenerator"/> carries them onto the generated DTO property
///     the same way it does any validation attribute.
/// </summary>
public class AttributeArgumentEmissionTests
{
    #region Constants

    private const string GenerateDtoAttributeSource = """
        using System;

        namespace DKNet.EfCore.DtoGenerator
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
            internal sealed class GenerateDtoAttribute : Attribute
            {
                public GenerateDtoAttribute(Type entityType) => EntityType = entityType;
                public Type EntityType { get; }
                public string[] Exclude { get; set; } = [];
                public string[] Include { get; set; } = [];
                public bool IgnoreComplexType { get; set; }
            }
        }
        """;

    private const string ProbeAttributesSource = """
        using System;

        namespace System.ComponentModel.DataAnnotations
        {
            public sealed class ProbeNullArgAttribute : Attribute
            {
                public ProbeNullArgAttribute(string tag) => Tag = tag;
                public string Tag { get; }
            }

            public sealed class ProbeTagsAttribute : Attribute
            {
                public ProbeTagsAttribute(string[] tags) => Tags = tags;
                public string[] Tags { get; }
            }

            public sealed class ProbeEnumArgAttribute : Attribute
            {
                public ProbeEnumArgAttribute(Probe.Enums.ProbeKind kind) => Kind = kind;
                public Probe.Enums.ProbeKind Kind { get; }
            }

            public sealed class ProbeTypeArgAttribute : Attribute
            {
                public ProbeTypeArgAttribute(Type type) => Type = type;
                public Type Type { get; }
            }

            public sealed class ProbeObjectArgAttribute : Attribute
            {
                public ProbeObjectArgAttribute(object value) => Value = value;
                public object Value { get; }
            }

            public sealed class ProbeTypeArrayArgAttribute : Attribute
            {
                public ProbeTypeArrayArgAttribute(Type[] types) => Types = types;
                public Type[] Types { get; }
            }

            public sealed class ProbeKindArrayArgAttribute : Attribute
            {
                public ProbeKindArrayArgAttribute(Probe.Enums.ProbeKind[] kinds) => Kinds = kinds;
                public Probe.Enums.ProbeKind[] Kinds { get; }
            }

            public sealed class ProbeFlagsArgAttribute : Attribute
            {
                public ProbeFlagsArgAttribute(Probe.Enums.ProbeFlags flags) => Flags = flags;
                public Probe.Enums.ProbeFlags Flags { get; }
            }

            public sealed class ProbeDoubleArgAttribute : Attribute
            {
                public ProbeDoubleArgAttribute(double value) => Value = value;
                public double Value { get; }
            }

            public sealed class ProbeBoolArgAttribute : Attribute
            {
                public ProbeBoolArgAttribute(bool value) => Value = value;
                public bool Value { get; }
            }

            public sealed class ProbeFloatArgAttribute : Attribute
            {
                public ProbeFloatArgAttribute(float value) => Value = value;
                public float Value { get; }
            }

            public sealed class ProbeCharArgAttribute : Attribute
            {
                public ProbeCharArgAttribute(char value) => Value = value;
                public char Value { get; }
            }
        }

        namespace Probe.Enums
        {
            public enum ProbeKind
            {
                None = 0,
                First = 1,
                Second = 2,
            }

            [Flags]
            public enum ProbeFlags
            {
                None = 0,
                First = 1,
                Second = 2,
            }
        }

        namespace Probe.Markers
        {
            public sealed class Marker
            {
            }
        }
        """;

    private const string NullArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NullArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeNullArg(null)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NonParamsArrayEntitySource = """
        namespace Probe.Entities
        {
            public sealed class TaggedProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTags(new[] { "a", "b" })]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string EmptyNonParamsArrayEntitySource = """
        namespace Probe.Entities
        {
            public sealed class EmptyTagsProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTags(new string[0])]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NullElementNonParamsArrayEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NullElementTagsProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTags(new string[] { null })]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string EnumArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class EnumArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeEnumArg(Probe.Enums.ProbeKind.Second)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string TypeofOnTypeParamEntitySource = """
        namespace Probe.Entities
        {
            public sealed class TypeofOnTypeParamProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTypeArg(typeof(Probe.Markers.Marker))]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string TypeofOnObjectParamEntitySource = """
        namespace Probe.Entities
        {
            public sealed class TypeofOnObjectParamProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeObjectArg(typeof(Probe.Markers.Marker))]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string TypeofInArrayEntitySource = """
        namespace Probe.Entities
        {
            public sealed class TypeofInArrayProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeTypeArrayArg(new[] { typeof(Probe.Markers.Marker) })]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NonKeywordArrayElementEntitySource = """
        namespace Probe.Entities
        {
            public sealed class KindArrayProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeKindArrayArg(new[] { Probe.Enums.ProbeKind.First })]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string FlagsArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class FlagsArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeFlagsArg(Probe.Enums.ProbeFlags.First | Probe.Enums.ProbeFlags.Second)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string DoubleArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class DoubleArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeDoubleArg(1.5)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string BoolTrueArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class BoolTrueArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeBoolArg(true)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string BoolFalseArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class BoolFalseArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeBoolArg(false)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string FloatArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class FloatArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeFloatArg(1.5f)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NaNDoubleArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NaNDoubleArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeDoubleArg(double.NaN)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string PositiveInfinityDoubleArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class PositiveInfinityDoubleArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeDoubleArg(double.PositiveInfinity)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NegativeInfinityDoubleArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NegativeInfinityDoubleArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeDoubleArg(double.NegativeInfinity)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NaNFloatArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NaNFloatArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeFloatArg(float.NaN)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string PositiveInfinityFloatArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class PositiveInfinityFloatArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeFloatArg(float.PositiveInfinity)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string NegativeInfinityFloatArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class NegativeInfinityFloatArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeFloatArg(float.NegativeInfinity)]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string CharEscapeArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class CharEscapeArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeCharArg('\'')]
                public decimal Price { get; set; }
            }
        }
        """;

    private const string StringBackslashArgEntitySource = """
        namespace Probe.Entities
        {
            public sealed class StringBackslashArgProduct
            {
                public int ProductId { get; set; }

                [System.ComponentModel.DataAnnotations.ProbeNullArg("a\\b")]
                public decimal Price { get; set; }
            }
        }
        """;

    private static readonly MetadataReference[] References =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location),
    ];

    #endregion

    #region Methods

    [Fact]
    public void NullValuedArgument_GeneratedSource_CompilesWithNoWarningOrAboveDiagnostic()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NullArgProduct", "NullArgProductDto");

        // Act
        var diagnostics = Compile(NullArgEntitySource, dtoSource);

        // Assert
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
        diagnostics.ShouldNotContain(d => d.Id == "CS8625");
    }

    [Fact]
    public void NonParamsArrayArgument_IsEmittedAsArrayCreation_NotSpread()
    {
        // Arrange
        var dtoSource = BuildDtoSource("TaggedProduct", "TaggedProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NonParamsArrayEntitySource, dtoSource);

        // Assert
        source.ShouldContain("new string[] {");
        source.ShouldNotContain("ProbeTags(\"a\", \"b\")");
        diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty(
            string.Join('\n', diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void EmptyNonParamsArrayArgument_GeneratedSource_CompilesWithNoWarningOrAboveDiagnostic()
    {
        // Arrange
        var dtoSource = BuildDtoSource("EmptyTagsProduct", "EmptyTagsProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(EmptyNonParamsArrayEntitySource, dtoSource);

        // Assert
        source.ShouldContain("new string[] {");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void NullElementInNonParamsArrayArgument_GeneratedSource_CompilesWithNoWarningOrAboveDiagnostic()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NullElementTagsProduct", "NullElementTagsProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NullElementNonParamsArrayEntitySource, dtoSource);

        // Assert
        source.ShouldContain("new string[] {");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void EnumArgument_EmitsQualifiedMemberName_NotUnderlyingInteger()
    {
        // Arrange
        var dtoSource = BuildDtoSource("EnumArgProduct", "EnumArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(EnumArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("Probe.Enums.ProbeKind.Second");
        source.ShouldNotContain("ProbeKind.2");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void TypeofArgument_OnTypeTypedParameter_EmitsQualifiedTypeofExpression()
    {
        // Arrange
        var dtoSource = BuildDtoSource("TypeofOnTypeParamProduct", "TypeofOnTypeParamProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(TypeofOnTypeParamEntitySource, dtoSource);

        // Assert
        source.ShouldContain("typeof(global::Probe.Markers.Marker)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void FlagsCombinationArgument_NoMatchingMember_EmitsQualifiedCast()
    {
        // Arrange
        var dtoSource = BuildDtoSource("FlagsArgProduct", "FlagsArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(FlagsArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("((global::Probe.Enums.ProbeFlags)3)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void DoubleArgument_UnderCommaDecimalCulture_EmitsInvariantFormatting()
    {
        // Arrange
        var dtoSource = BuildDtoSource("DoubleArgProduct", "DoubleArgProductDto");

        // Act — the comma-decimal culture is confined to a dedicated, throwaway thread (R5): it never
        // touches this test-runner thread's own CurrentCulture, so it cannot leak into a sibling fact
        // that happens to reuse the same pooled thread under xUnit parallelisation.
        var (source, diagnostics) = CompileUnderCulture(
            DoubleArgEntitySource, dtoSource, CultureInfo.GetCultureInfo("de-DE"));

        // Assert
        source.ShouldContain("ProbeDoubleArg(1.5)");
        source.ShouldNotContain("1,5");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void BooleanArgument_True_EmitsLowercaseKeyword()
    {
        // Arrange
        var dtoSource = BuildDtoSource("BoolTrueArgProduct", "BoolTrueArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(BoolTrueArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeBoolArg(true)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void BooleanArgument_False_EmitsLowercaseKeyword()
    {
        // Arrange
        var dtoSource = BuildDtoSource("BoolFalseArgProduct", "BoolFalseArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(BoolFalseArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeBoolArg(false)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void FloatArgument_EmitsSuffixedLiteral_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("FloatArgProduct", "FloatArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(FloatArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeFloatArg(1.5f)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void NaNDoubleArgument_EmitsConstantReference_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NaNDoubleArgProduct", "NaNDoubleArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NaNDoubleArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeDoubleArg(double.NaN)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void PositiveInfinityDoubleArgument_EmitsConstantReference_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("PositiveInfinityDoubleArgProduct", "PositiveInfinityDoubleArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(PositiveInfinityDoubleArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeDoubleArg(double.PositiveInfinity)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void NegativeInfinityDoubleArgument_EmitsConstantReference_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NegativeInfinityDoubleArgProduct", "NegativeInfinityDoubleArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NegativeInfinityDoubleArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeDoubleArg(double.NegativeInfinity)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void NaNFloatArgument_EmitsConstantReference_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NaNFloatArgProduct", "NaNFloatArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NaNFloatArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeFloatArg(float.NaN)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void PositiveInfinityFloatArgument_EmitsConstantReference_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("PositiveInfinityFloatArgProduct", "PositiveInfinityFloatArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(PositiveInfinityFloatArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeFloatArg(float.PositiveInfinity)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void NegativeInfinityFloatArgument_EmitsConstantReference_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("NegativeInfinityFloatArgProduct", "NegativeInfinityFloatArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NegativeInfinityFloatArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeFloatArg(float.NegativeInfinity)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void CharArgument_NeedingEscape_EmitsEscapedLiteral_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("CharEscapeArgProduct", "CharEscapeArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(CharEscapeArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeCharArg('\\'')");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void StringArgument_ContainingBackslash_EmitsEscapedLiteral_AndCompiles()
    {
        // Arrange
        var dtoSource = BuildDtoSource("StringBackslashArgProduct", "StringBackslashArgProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(StringBackslashArgEntitySource, dtoSource);

        // Assert
        source.ShouldContain("ProbeNullArg(\"a\\\\b\")");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void TypeofArgument_OnObjectTypedParameter_EmitsQualifiedTypeofExpression()
    {
        // Arrange
        var dtoSource = BuildDtoSource("TypeofOnObjectParamProduct", "TypeofOnObjectParamProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(TypeofOnObjectParamEntitySource, dtoSource);

        // Assert
        source.ShouldContain("typeof(global::Probe.Markers.Marker)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void TypeofArgument_InsideArray_EmitsQualifiedTypeofExpression()
    {
        // Arrange
        var dtoSource = BuildDtoSource("TypeofInArrayProduct", "TypeofInArrayProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(TypeofInArrayEntitySource, dtoSource);

        // Assert
        source.ShouldContain("typeof(global::Probe.Markers.Marker)");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    [Fact]
    public void NonKeywordArrayElementType_GeneratedSource_CompilesWithNoWarningOrAboveDiagnostic()
    {
        // Arrange
        var dtoSource = BuildDtoSource("KindArrayProduct", "KindArrayProductDto");

        // Act
        var (source, diagnostics) = CompileAndCaptureSource(NonKeywordArrayElementEntitySource, dtoSource);

        // Assert
        source.ShouldContain("Probe.Enums.ProbeKind[] {");
        var atOrAboveWarning = diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToList();
        atOrAboveWarning.ShouldBeEmpty(string.Join('\n', atOrAboveWarning.Select(d => d.ToString())));
    }

    #endregion

    #region Internals

    private static string BuildDtoSource(string entityName, string dtoName) =>
        $$"""
        using DKNet.EfCore.DtoGenerator;

        namespace Probe.Dtos
        {
            [GenerateDto(typeof(Probe.Entities.{{entityName}}))]
            public partial record {{dtoName}};
        }
        """;

    private static List<Diagnostic> Compile(string entitySource, string dtoSource) =>
        CompileAndCaptureSource(entitySource, dtoSource).Diagnostics;

    /// <summary>
    /// Runs <see cref="CompileAndCaptureSource"/> on a dedicated, throwaway thread with
    /// <see cref="CultureInfo.CurrentCulture"/> set to <paramref name="culture"/> for that thread only
    /// (R5) — the calling (test-runner) thread's own culture is never touched, so no sibling fact can
    /// observe the mutation even under xUnit thread-pool reuse.
    /// </summary>
    private static (string Source, List<Diagnostic> Diagnostics) CompileUnderCulture(
        string entitySource, string dtoSource, CultureInfo culture)
    {
        (string Source, List<Diagnostic> Diagnostics) result = default;
        var worker = new Thread(() =>
        {
            CultureInfo.CurrentCulture = culture;
            result = CompileAndCaptureSource(entitySource, dtoSource);
        });
        worker.Start();
        worker.Join();
        return result;
    }

    private static (string Source, List<Diagnostic> Diagnostics) CompileAndCaptureSource(
        string entitySource, string dtoSource)
    {
        var compilation = CSharpCompilation.Create(
            "ProbeCompilation",
            [
                CSharpSyntaxTree.ParseText(GenerateDtoAttributeSource),
                CSharpSyntaxTree.ParseText(ProbeAttributesSource),
                CSharpSyntaxTree.ParseText(entitySource),
                CSharpSyntaxTree.ParseText(dtoSource),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new DKNet.EfCore.DtoGenerator.DtoGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator], optionsProvider: new TestAnalyzerConfigOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out _);
        var runResult = ((CSharpGeneratorDriver)driver).GetRunResult();

        var generatedSource = string.Join('\n', runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));
        generatedSource.ShouldNotBeNullOrEmpty("generator should have produced DTO source");

        // Diagnostics are scoped to the generator's own output trees, not the whole compilation — a
        // hand-authored [ProbeNullArg(null)] on the entity is the caller's own pre-existing warning
        // (unrelated to this generator) and must not be conflated with what the generator emits.
        var generatedTrees = runResult.GeneratedTrees.ToHashSet();
        var generatedSourceDiagnostics = updatedCompilation.GetDiagnostics()
            .Where(d => d.Location.SourceTree is not null && generatedTrees.Contains(d.Location.SourceTree))
            .ToList();

        return (generatedSource, generatedSourceDiagnostics);
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions _globalOptions = new();

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return false;
        }
    }

    #endregion
}
