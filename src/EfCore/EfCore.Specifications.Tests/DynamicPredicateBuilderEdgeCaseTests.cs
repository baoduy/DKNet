// <copyright file="DynamicPredicateBuilderEdgeCaseTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Dynamics;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Edge-case tests for the internal helpers in <see cref="DynamicPredicateBuilderExtensions" />.
///     These tests exercise the security/validation branches that the higher-level
///     <c>DynamicAnd</c>/<c>DynamicOr</c> tests do not hit directly: null/empty/oversized property
///     names, expressions containing dangerous patterns, and <c>ValidateArrayValue</c> rejections.
/// </summary>
public class DynamicPredicateBuilderEdgeCaseTests
{
    #region IsValidPropertyName

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsValidPropertyName_NullOrWhitespace_ReturnsFalse(string? input)
        => DynamicPredicateBuilderExtensions.IsValidPropertyName(input).ShouldBeFalse();

    [Fact]
    public void IsValidPropertyName_ExceedsMaxLength_ReturnsFalse()
    {
        // 257 chars triggers the > 256 guard
        var oversized = new string('A', 257);
        DynamicPredicateBuilderExtensions.IsValidPropertyName(oversized).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Name; DROP TABLE")] // semicolon
    [InlineData("Name OR 1=1")]      // spaces and equals
    [InlineData("Name()")]            // parentheses
    [InlineData("Name[0]")]           // brackets
    [InlineData("Name+Other")]        // plus
    public void IsValidPropertyName_DangerousCharacters_ReturnsFalse(string input)
        => DynamicPredicateBuilderExtensions.IsValidPropertyName(input).ShouldBeFalse();

    [Theory]
    [InlineData("Name")]
    [InlineData("Category.Name")]
    [InlineData("snake_case")]
    [InlineData("kebab-case")] // hyphens are allowed pre-normalization (ToPascalCase strips them)
    public void IsValidPropertyName_ValidPaths_ReturnsTrue(string input)
        => DynamicPredicateBuilderExtensions.IsValidPropertyName(input).ShouldBeTrue();

    #endregion

    #region ValidateExpression

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateExpression_NullOrWhitespace_Throws(string? input)
        => Should.Throw<ArgumentException>(() => DynamicPredicateBuilderExtensions.ValidateExpression(input!));

    [Theory]
    [InlineData("System.IO.File.Delete(\"/etc/passwd\")")]
    [InlineData("typeof(Object).GetMethods()")]
    [InlineData("Activator.CreateInstance(typeof(object))")]
    [InlineData("Environment.Exit(0)")]
    [InlineData("Process.Start(\"cmd\")")]
    [InlineData("Assembly.Load(\"evil\")")]
    [InlineData("Task.Run(() => 1)")]
    [InlineData("AppDomain.CurrentDomain")]
    [InlineData("Marshal.Copy(IntPtr.Zero, null, 0, 0)")]
    public void ValidateExpression_DangerousPattern_Throws(string expression)
    {
        var ex = Should.Throw<ArgumentException>(
            () => DynamicPredicateBuilderExtensions.ValidateExpression(expression));
        ex.Message.ShouldContain("disallowed pattern");
        ex.ParamName.ShouldBe("expression");
    }

    [Theory]
    [InlineData("Name == @0")]
    [InlineData("Price > @0 AND IsActive")]
    [InlineData("Category.Name.Contains(@0)")]
    public void ValidateExpression_SafeExpression_DoesNotThrow(string expression)
        => Should.NotThrow(() => DynamicPredicateBuilderExtensions.ValidateExpression(expression));

    #endregion

    #region ValidateArrayValue

    [Theory]
    [InlineData(Ops.In)]
    [InlineData(Ops.NotIn)]
    public void ValidateArrayValue_NullValue_ReturnsFalse(Ops op)
        => DynamicPredicateBuilderExtensions.ValidateArrayValue(null, op).ShouldBeFalse();

    [Theory]
    [InlineData(Ops.In)]
    [InlineData(Ops.NotIn)]
    public void ValidateArrayValue_StringValue_ReturnsFalse(Ops op)
        => DynamicPredicateBuilderExtensions.ValidateArrayValue("a string", op).ShouldBeFalse();

    [Theory]
    [InlineData(Ops.In)]
    [InlineData(Ops.NotIn)]
    public void ValidateArrayValue_NonEnumerableValue_ReturnsFalse(Ops op)
        => DynamicPredicateBuilderExtensions.ValidateArrayValue(42, op).ShouldBeFalse();

    [Theory]
    [InlineData(Ops.In)]
    [InlineData(Ops.NotIn)]
    public void ValidateArrayValue_EmptyCollection_ReturnsFalse(Ops op)
        => DynamicPredicateBuilderExtensions.ValidateArrayValue(Array.Empty<int>(), op).ShouldBeFalse();

    [Theory]
    [InlineData(Ops.In)]
    [InlineData(Ops.NotIn)]
    public void ValidateArrayValue_NonEmptyCollection_ReturnsTrue(Ops op)
        => DynamicPredicateBuilderExtensions.ValidateArrayValue(new[] { 1, 2 }, op).ShouldBeTrue();

    [Theory]
    [InlineData(Ops.Equal)]
    [InlineData(Ops.GreaterThan)]
    [InlineData(Ops.Contains)]
    public void ValidateArrayValue_NonInOperations_AlwaysReturnsTrue(Ops op)
    {
        DynamicPredicateBuilderExtensions.ValidateArrayValue(null, op).ShouldBeTrue();
        DynamicPredicateBuilderExtensions.ValidateArrayValue("anything", op).ShouldBeTrue();
        DynamicPredicateBuilderExtensions.ValidateArrayValue(Array.Empty<int>(), op).ShouldBeTrue();
    }

    #endregion
}
