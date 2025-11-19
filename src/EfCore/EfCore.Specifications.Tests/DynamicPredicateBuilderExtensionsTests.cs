// <copyright file="DynamicPredicateBuilderExtensionsTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>


using DKNet.EfCore.Specifications.Dynamics;

namespace EfCore.Specifications.Tests;

/// <summary>
///     Unit tests for DynamicPredicateBuilderExtensions internal methods
/// </summary>
public class DynamicPredicateBuilderExtensionsTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void AdjustOperationForValueType_BoolType_ConvertsContainsToEqual()
    {
        // Arrange
        var boolType = typeof(bool);

        // Act & Assert
        boolType.AdjustOperationForValueType(DynamicOperations.Contains).ShouldBe(DynamicOperations.Equal);
    }

    [Fact]
    public void AdjustOperationForValueType_ComparisonOperations_RemainsUnchanged()
    {
        // Arrange
        var intType = typeof(int);

        // Act & Assert: Comparison operations should not change
        intType.AdjustOperationForValueType(DynamicOperations.Equal).ShouldBe(DynamicOperations.Equal);
        intType.AdjustOperationForValueType(DynamicOperations.NotEqual).ShouldBe(DynamicOperations.NotEqual);
        intType.AdjustOperationForValueType(DynamicOperations.GreaterThan).ShouldBe(DynamicOperations.GreaterThan);
        intType.AdjustOperationForValueType(DynamicOperations.LessThan).ShouldBe(DynamicOperations.LessThan);
    }

    [Fact]
    public void AdjustOperationForValueType_DecimalType_ConvertsContainsToEqual()
    {
        // Arrange
        var decimalType = typeof(decimal);

        // Act & Assert
        decimalType.AdjustOperationForValueType(DynamicOperations.Contains).ShouldBe(DynamicOperations.Equal);
    }

    [Fact]
    public void AdjustOperationForValueType_EnumType_ConvertsContainsToEqual()
    {
        // Arrange
        var enumType = typeof(OrderStatus);

        // Act & Assert
        enumType.AdjustOperationForValueType(DynamicOperations.Contains).ShouldBe(DynamicOperations.Equal);
        enumType.AdjustOperationForValueType(DynamicOperations.NotContains).ShouldBe(DynamicOperations.NotEqual);
    }

    [Fact]
    public void AdjustOperationForValueType_IntType_ConvertsContainsToEqual()
    {
        // Arrange
        var intType = typeof(int);

        // Act & Assert
        intType.AdjustOperationForValueType(DynamicOperations.Contains).ShouldBe(DynamicOperations.Equal);
        intType.AdjustOperationForValueType(DynamicOperations.NotContains).ShouldBe(DynamicOperations.NotEqual);
        intType.AdjustOperationForValueType(DynamicOperations.StartsWith).ShouldBe(DynamicOperations.Equal);
        intType.AdjustOperationForValueType(DynamicOperations.EndsWith).ShouldBe(DynamicOperations.Equal);
    }

    [Fact]
    public void AdjustOperationForValueType_NullableStringType_ReturnsOriginalOperation()
    {
        // Arrange
        var nullableStringType = typeof(string);

        // Act & Assert
        nullableStringType.AdjustOperationForValueType(DynamicOperations.Contains).ShouldBe(DynamicOperations.Contains);
    }

    [Fact]
    public void AdjustOperationForValueType_NullType_ReturnsOriginalOperation()
    {
        // Arrange
        Type? nullType = null;

        // Act & Assert
        nullType.AdjustOperationForValueType(DynamicOperations.Contains).ShouldBe(DynamicOperations.Contains);
    }

    [Fact]
    public void AdjustOperationForValueType_StringType_ReturnsOriginalOperation()
    {
        // Arrange
        var stringType = typeof(string);

        // Act & Assert: String operations should remain unchanged
        stringType.AdjustOperationForValueType(DynamicOperations.Contains).ShouldBe(DynamicOperations.Contains);
        stringType.AdjustOperationForValueType(DynamicOperations.StartsWith).ShouldBe(DynamicOperations.StartsWith);
        stringType.AdjustOperationForValueType(DynamicOperations.EndsWith).ShouldBe(DynamicOperations.EndsWith);
        stringType.AdjustOperationForValueType(DynamicOperations.NotContains).ShouldBe(DynamicOperations.NotContains);
    }

    [Fact]
    public void BuildClause_Contains_GeneratesCorrectClause()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.Contains, "test", 0);

        // Assert
        clause.ShouldBe("Name.Contains(@0)");
    }

    [Fact]
    public void BuildClause_EndsWith_GeneratesCorrectClause()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.EndsWith, "test", 0);

        // Assert
        clause.ShouldBe("Name.EndsWith(@0)");
    }

    [Fact]
    public void BuildClause_Equal_WithNull_GeneratesNullCheck()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.Equal, null, 0);

        // Assert
        clause.ShouldBe("Name == null");
    }

    [Fact]
    public void BuildClause_Equal_WithValue_GeneratesCorrectClause()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.Equal, "test", 0);

        // Assert
        clause.ShouldBe("Name == @0");
    }

    [Fact]
    public void BuildClause_GreaterThan_GeneratesCorrectClause()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Price", DynamicOperations.GreaterThan, 100, 0);

        // Assert
        clause.ShouldBe("Price > @0");
    }

    [Fact]
    public void BuildClause_LessThan_GeneratesCorrectClause()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Price", DynamicOperations.LessThan, 100, 0);

        // Assert
        clause.ShouldBe("Price < @0");
    }

    [Fact]
    public void BuildClause_MultipleParameters_UsesCorrectIndex()
    {
        // Arrange & Act
        var clause1 = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.Equal, "test", 0);
        var clause2 = DynamicPredicateBuilderExtensions.BuildClause("Price", DynamicOperations.GreaterThan, 100, 1);
        var clause3 = DynamicPredicateBuilderExtensions.BuildClause("IsActive", DynamicOperations.Equal, true, 2);

        // Assert
        clause1.ShouldBe("Name == @0");
        clause2.ShouldBe("Price > @1");
        clause3.ShouldBe("IsActive == @2");
    }

    [Fact]
    public void BuildClause_NotContains_GeneratesCorrectClause()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.NotContains, "test", 0);

        // Assert
        clause.ShouldBe("!Name.Contains(@0)");
    }

    [Fact]
    public void BuildClause_NotEqual_WithNull_GeneratesNotNullCheck()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.NotEqual, null, 0);

        // Assert
        clause.ShouldBe("Name != null");
    }

    [Fact]
    public void BuildClause_StartsWith_GeneratesCorrectClause()
    {
        // Arrange & Act
        var clause = DynamicPredicateBuilderExtensions.BuildClause("Name", DynamicOperations.StartsWith, "test", 0);

        // Assert
        clause.ShouldBe("Name.StartsWith(@0)");
    }

    [Fact]
    public void BuildClause_UnsupportedOperation_ThrowsException()
    {
        // Arrange & Act & Assert
        Should.Throw<NotSupportedException>(() =>
            DynamicPredicateBuilderExtensions.BuildClause("Name", (DynamicOperations)999, "test", 0));
    }

    [Fact]
    public void DynamicAnd_CaseInsensitivePropertyResolution_WorksCorrectly()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(true);

        // Act: Test case-insensitive property names
        var result = predicate
            .DynamicAnd("name", DynamicOperations.Contains, "test") // lowercase
            .DynamicAnd("PRICE", DynamicOperations.GreaterThan, 100) // uppercase
            .DynamicAnd("IsActive", DynamicOperations.Equal, true); // PascalCase

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("[Name]");
        sql.ShouldContain("[Price]");
        sql.ShouldContain("[IsActive]");
    }

    [Fact]
    public void DynamicAnd_WithInvalidEnumValue_ReturnsUnchangedPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Order>(o => o.Id > 0);

        // Act: Try to filter with invalid enum value
        var result = predicate.DynamicAnd("Status", DynamicOperations.Equal, "InvalidEnumValue");

        // Assert: Should ignore invalid enum
        var query = _context.Orders.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("[Status] =");
    }

    [Fact]
    public void DynamicAnd_WithInvalidPropertyName_ReturnsUnchangedPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act
        var result = predicate.DynamicAnd("NonExistentProperty", DynamicOperations.Equal, "test");

        // Assert: Should return original predicate unchanged
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("NonExistent");
        sql.ShouldContain("[IsActive]");
    }

    [Fact]
    public void DynamicOr_WithInvalidPropertyName_ReturnsUnchangedPredicate()
    {
        // Arrange
        var predicate = PredicateBuilder.New<Product>(p => p.IsActive);

        // Act
        var result = predicate.DynamicOr("NonExistentProperty", DynamicOperations.Equal, "test");

        // Assert
        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("NonExistent");
    }

    [Fact]
    public void ResolvePropertyType_InvalidNestedProperty_ReturnsNull()
    {
        // Arrange
        var productType = typeof(Product);

        // Act
        var result = productType.ResolvePropertyType("Category.NonExistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolvePropertyType_InvalidProperty_ReturnsNull()
    {
        // Arrange
        var productType = typeof(Product);

        // Act
        var result = productType.ResolvePropertyType("NonExistentProperty");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolvePropertyType_NestedProperty_ReturnsType()
    {
        // Arrange
        var productType = typeof(Product);

        // Act: Resolve nested property type
        var categoryNameType = productType.ResolvePropertyType("Category.Name");

        // Assert
        categoryNameType.ShouldBe(typeof(string));
    }

    [Fact]
    public void ResolvePropertyType_ValidProperty_ReturnsType()
    {
        // Arrange: Use Product entity to test type resolution
        var productType = typeof(Product);

        // Act: Resolve simple property type
        var nameType = productType.ResolvePropertyType("Name");
        var priceType = productType.ResolvePropertyType("Price");
        var isActiveType = productType.ResolvePropertyType("IsActive");

        // Assert
        nameType.ShouldBe(typeof(string));
        priceType.ShouldBe(typeof(decimal));
        isActiveType.ShouldBe(typeof(bool));
    }

    [Fact]
    public void ValidateEnumValue_InvalidEnumValue_ReturnsFalse()
    {
        // Arrange
        var enumType = typeof(OrderStatus);
        var invalidValue = "InvalidStatus";

        // Act
        var result = enumType.ValidateEnumValue(invalidValue);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateEnumValue_NonEnumType_ReturnsTrue()
    {
        // Arrange
        var intType = typeof(int);

        // Act
        var result = intType.ValidateEnumValue(123);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateEnumValue_NullForNonNullableEnum_ReturnsFalse()
    {
        // Arrange
        var enumType = typeof(OrderStatus);

        // Act
        var result = enumType.ValidateEnumValue(null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateEnumValue_NullForNullableEnum_ReturnsTrue()
    {
        // Arrange
        var nullableEnumType = typeof(OrderStatus?);

        // Act
        var result = nullableEnumType.ValidateEnumValue(null);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateEnumValue_NullType_ReturnsTrue()
    {
        // Arrange
        Type? nullType = null;

        // Act
        var result = nullType.ValidateEnumValue("anyValue");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateEnumValue_ValidEnumValue_ReturnsTrue()
    {
        // Arrange
        var enumType = typeof(OrderStatus);
        var validValue = OrderStatus.Pending;

        // Act
        var result = enumType.ValidateEnumValue(validValue);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateEnumValue_ValidEnumValueAsInt_ReturnsTrue()
    {
        // Arrange
        var enumType = typeof(OrderStatus);
        var validValue = 1; // Valid enum value

        // Act
        var result = enumType.ValidateEnumValue(validValue);

        // Assert
        result.ShouldBeTrue();
    }

    #endregion
}