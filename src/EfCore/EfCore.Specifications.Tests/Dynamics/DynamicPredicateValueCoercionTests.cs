// <copyright file="DynamicPredicateValueCoercionTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>


using DKNet.EfCore.Specifications.Dynamics;

namespace EfCore.Specifications.Tests.Dynamics;

/// <summary>
///     BDD-style tests verifying value coercion behaviour in dynamic predicates
///     (DRK-39 acceptance criteria).
/// </summary>
public class DynamicPredicateValueCoercionTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context = fixture.Db!;

    #endregion

    #region AC 1 — Convertible string for numeric property

    [Fact]
    public void DynamicAnd_WithStringValueForDecimalProperty_CoercesAndFiltersCorrectly()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("Price", Ops.GreaterThan, "100");

        var query = _context.Products.AsExpandable().Where(result);
        var products = query.ToList();
        products.ShouldAllBe(p => p.Price > 100m);
    }

    [Fact]
    public void DynamicAnd_WithStringValueForIntProperty_CoercesAndFiltersCorrectly()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("StockQuantity", Ops.GreaterThanOrEqual, "10");

        var query = _context.Products.AsExpandable().Where(result);
        var products = query.ToList();
        products.ShouldAllBe(p => p.StockQuantity >= 10);
    }

    #endregion

    #region AC 2 — Unconvertible string for numeric property

    [Fact]
    public void DynamicAnd_WithUnconvertibleStringForDecimalProperty_ReturnsUnchangedPredicate()
    {
        var basePredicate = PredicateBuilder.New<Product>(p => p.IsActive);

        var result = basePredicate.DynamicAnd("Price", Ops.GreaterThan, "abc");

        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        var normalizedSql = NormalizeSql(sql);
        normalizedSql.ShouldNotContain("abc");
        normalizedSql.ShouldContain("isactive", Case.Insensitive);
    }

    [Fact]
    public void DynamicAnd_WithUnconvertibleStringForIntProperty_ReturnsUnchangedPredicate()
    {
        var basePredicate = PredicateBuilder.New<Product>(p => p.IsActive);

        var result = basePredicate.DynamicAnd("StockQuantity", Ops.LessThan, "xyz");

        var query = _context.Products.AsExpandable().Where(result);
        var sql = NormalizeSql(query.ToQueryString());
        sql.ShouldContain("isactive", Case.Insensitive);
        sql.ShouldNotContain("xyz");
    }

    #endregion

    #region AC 3 — Value already of correct type (no regression)

    [Fact]
    public async Task DynamicAnd_WithCorrectlyTypedDecimalValue_IsUnaffected()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate
            .DynamicAnd("Price", Ops.GreaterThan, 100m)
            .DynamicAnd("Price", Ops.LessThan, 1000m);

        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.Price > 100m && p.Price < 1000m);
    }

    [Fact]
    public async Task DynamicAnd_WithCorrectlyTypedIntValue_IsUnaffected()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("StockQuantity", Ops.Equal, 50);

        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.StockQuantity == 50);
    }

    [Fact]
    public async Task DynamicAnd_WithCorrectlyTypedBoolValue_IsUnaffected()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("IsActive", Ops.Equal, true);

        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.IsActive);
    }

    #endregion

    #region AC 4 — Convertible string for DateTime

    [Fact]
    public void DynamicAnd_WithStringValueForDateTimeProperty_CoercesAndFiltersCorrectly()
    {
        var predicate = PredicateBuilder.New<Product>(true);
        var targetDate = new DateTime(2026, 1, 1);

        var result = predicate.DynamicAnd("CreatedDate", Ops.GreaterThanOrEqual, "2026-01-01");

        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("2026-01-01");

        var products = query.ToList();
        products.ShouldAllBe(p => p.CreatedDate >= targetDate);
    }

    #endregion

    #region AC 5 — Null value semantics preserved

    [Fact]
    public void DynamicAnd_WithNullValueForNullableProperty_PreservesIsNullSemantics()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("Description", Ops.Equal, null);

        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("\"Description\" IS NULL");
    }

    [Fact]
    public void DynamicAnd_WithNullValueForNullableStringProperty_NotEqual_GeneratesIsNotNull()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("Description", Ops.NotEqual, null);

        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldContain("\"Description\" IS NOT NULL");
    }

    #endregion

    #region AC 6 — SQL parameterization preserved

    [Fact]
    public void DynamicAnd_WithCoercedStringValue_ParameterizesSQLNotInterpolated()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("Price", Ops.GreaterThan, "100");

        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();

        sql.ShouldContain("100");
        sql.ShouldNotContain("'100'");
    }

    [Fact]
    public void DynamicAnd_WithCoercedDateTimeString_ParameterizesSQLNotInterpolated()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("CreatedDate", Ops.GreaterThanOrEqual, "2026-01-01");

        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();

        sql.ShouldContain("2026-01-01");
        sql.ShouldNotContain("'2026-01-01'");
    }

    [Fact]
    public async Task DynamicAnd_WithStringForBoolProperty_CoercesAndParameterizes()
    {
        var predicate = PredicateBuilder.New<Product>(true);

        var result = predicate.DynamicAnd("IsActive", Ops.Equal, "true");

        var query = _context.Products.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        var products = await query.ToListAsync();

        products.ShouldAllBe(p => p.IsActive);
        sql.ShouldNotContain("'true'");
        sql.ShouldNotContain("'True'");
    }

    #endregion

    #region Regression — int[] to enum In filter (DRK-47)

    [Fact]
    public void DynamicAnd_WithIntArrayForEnumInFilter_SkipsConditionGracefully()
    {
        var statusValues = new[] { 0, 1, 2, 3 };
        var basePredicate = PredicateBuilder.New<Order>(o => o.TotalAmount > 0);

        var result = basePredicate.DynamicAnd("Status", Ops.In, statusValues);

        var query = _context.Orders.AsExpandable().Where(result);
        var sql = query.ToQueryString();
        sql.ShouldNotContain("IN");
        sql.ShouldContain("\"TotalAmount\"");
    }

    #endregion

    #region String collection to In filter

    [Fact]
    public async Task DynamicAnd_WithStringArrayForEnumInFilter_CoercesElementsAndFilters()
    {
        // A string[] is what a query string yields, so unlike the int[] case above it IS coerced element-wise
        // — to OrderStatus[] — rather than skipped. Skipping instead would silently widen the result set to
        // every order, and leaving it uncoerced would throw at Dynamic LINQ parse time on Contains().
        var statusValues = new[] { nameof(OrderStatus.Pending), nameof(OrderStatus.Shipped) };
        var predicate = PredicateBuilder.New<Order>(true);

        var result = predicate.DynamicAnd("Status", Ops.In, statusValues);

        var query = _context.Orders.AsExpandable().Where(result);
        var orders = await query.ToListAsync();

        orders.ShouldAllBe(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Shipped);
        query.ToQueryString().ShouldContain("IN");
    }

    [Fact]
    public async Task DynamicAnd_WithEnumNameString_CoercesAndFilters()
    {
        // The name is what an API surface serializes, so it is what a caller filters by — but the enum
        // conversion this pipeline used went through Convert.ChangeType, which only reads the numeric form.
        var predicate = PredicateBuilder.New<Order>(true);

        var result = predicate.DynamicAnd("Status", Ops.Equal, nameof(OrderStatus.Shipped));

        var orders = await _context.Orders.AsExpandable().Where(result).ToListAsync();

        orders.ShouldNotBeEmpty();
        orders.ShouldAllBe(o => o.Status == OrderStatus.Shipped);
    }

    [Fact]
    public void DynamicAnd_WithUnparseableStringArrayForEnumInFilter_SkipsCondition()
    {
        var predicate = PredicateBuilder.New<Order>(o => o.TotalAmount > 0);

        var result = predicate.DynamicAnd("Status", Ops.In, new[] { "NotAStatus" });

        var sql = _context.Orders.AsExpandable().Where(result).ToQueryString();
        sql.ShouldNotContain("IN");
        sql.ShouldContain("\"TotalAmount\"");
    }

    #endregion

    #region DynamicOr coercion parity

    [Fact]
    public void DynamicOr_WithStringValueForDecimalProperty_CoercesAndCombines()
    {
        var predicate = PredicateBuilder.New<Product>(false);

        var result = predicate
            .DynamicOr("Price", Ops.GreaterThan, "500")
            .DynamicOr("Name", Ops.StartsWith, "Special");

        var query = _context.Products.AsExpandable().Where(result);
        var products = query.ToList();
        products.ShouldAllBe(p => p.Price > 500m || p.Name.StartsWith("Special"));
    }

    [Fact]
    public async Task DynamicOr_WithUnconvertibleString_ReturnsUnchangedPredicate()
    {
        var basePredicate = PredicateBuilder.New<Product>(p => p.IsActive);

        var result = basePredicate.DynamicOr("Price", Ops.GreaterThan, "not-a-number");

        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.IsActive);
    }

    #endregion

    #region TryCoerceValue — internal method unit tests

    [Fact]
    public void TryCoerceValue_NullValue_ReturnsTrueAndNull()
    {
        typeof(decimal).TryCoerceValue(null, out var coerced).ShouldBeTrue();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_AlreadyAssignableDecimal_ReturnsTrueWithOriginalValue()
    {
        typeof(decimal).TryCoerceValue(100m, out var coerced).ShouldBeTrue();
        coerced.ShouldBe(100m);
    }

    [Fact]
    public void TryCoerceValue_AlreadyAssignableInt_ReturnsTrueWithOriginalValue()
    {
        typeof(int).TryCoerceValue(42, out var coerced).ShouldBeTrue();
        coerced.ShouldBe(42);
    }

    [Fact]
    public void TryCoerceValue_AlreadyAssignableString_ReturnsTrueWithOriginalValue()
    {
        typeof(string).TryCoerceValue("hello", out var coerced).ShouldBeTrue();
        coerced.ShouldBe("hello");
    }

    [Fact]
    public void TryCoerceValue_EnumTypeWithValidStringValue_CoercesCorrectly()
    {
        typeof(OrderStatus).TryCoerceValue("0", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(OrderStatus.Pending);
    }

    [Fact]
    public void TryCoerceValue_EnumTypeWithValidIntValue_CoercesCorrectly()
    {
        typeof(OrderStatus).TryCoerceValue(1, out var coerced).ShouldBeTrue();
        coerced.ShouldBe(OrderStatus.Processing);
    }

    [Fact]
    public void TryCoerceValue_EnumTypeWithInvalidStringValue_ReturnsFalse()
    {
        typeof(OrderStatus).TryCoerceValue("NotAStatus", out var coerced).ShouldBeFalse();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_NonCoercibleNonEnumType_ReturnsTrueWithOriginalValue()
    {
        var complexObject = new Product();
        typeof(Product).TryCoerceValue(complexObject, out var coerced).ShouldBeTrue();
        coerced.ShouldBeSameAs(complexObject);
    }

    [Fact]
    public void TryCoerceValue_DecimalString_CoercesCorrectly()
    {
        typeof(decimal).TryCoerceValue("123.45", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(123.45m);
    }

    [Fact]
    public void TryCoerceValue_DecimalStringWithWhitespace_CoercesCorrectly()
    {
        typeof(decimal).TryCoerceValue(" 100 ", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(100m);
    }

    [Fact]
    public void TryCoerceValue_IntString_CoercesCorrectly()
    {
        typeof(int).TryCoerceValue("42", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(42);
    }

    [Fact]
    public void TryCoerceValue_DoubleString_CoercesCorrectly()
    {
        typeof(double).TryCoerceValue("3.14", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(3.14d);
    }

    [Fact]
    public void TryCoerceValue_BoolStringTrue_CoercesCorrectly()
    {
        typeof(bool).TryCoerceValue("true", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(true);
    }

    [Fact]
    public void TryCoerceValue_BoolStringFalse_CoercesCorrectly()
    {
        typeof(bool).TryCoerceValue("false", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(false);
    }

    [Fact]
    public void TryCoerceValue_DateTimeString_CoercesCorrectly()
    {
        typeof(DateTime).TryCoerceValue("2026-01-01", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(new DateTime(2026, 1, 1));
    }

    [Fact]
    public void TryCoerceValue_DateOnlyString_CoercesCorrectly()
    {
        typeof(DateOnly).TryCoerceValue("2026-06-15", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(new DateOnly(2026, 6, 15));
    }

    [Fact]
    public void TryCoerceValue_TimeOnlyString_CoercesCorrectly()
    {
        typeof(TimeOnly).TryCoerceValue("13:45:30", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(new TimeOnly(13, 45, 30));
    }

    [Fact]
    public void TryCoerceValue_GuidString_CoercesCorrectly()
    {
        var guid = Guid.NewGuid();
        typeof(Guid).TryCoerceValue(guid.ToString(), out var coerced).ShouldBeTrue();
        coerced.ShouldBe(guid);
    }

    [Fact]
    public void TryCoerceValue_InvalidDecimalString_ReturnsFalse()
    {
        typeof(decimal).TryCoerceValue("abc", out var coerced).ShouldBeFalse();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_InvalidIntString_ReturnsFalse()
    {
        typeof(int).TryCoerceValue("twelve", out var coerced).ShouldBeFalse();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_InvalidDateTimeString_ReturnsFalse()
    {
        typeof(DateTime).TryCoerceValue("not-a-date", out var coerced).ShouldBeFalse();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_InvalidGuidString_ReturnsFalse()
    {
        typeof(Guid).TryCoerceValue("not-a-guid", out var coerced).ShouldBeFalse();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_OverflowIntString_ReturnsFalse()
    {
        typeof(int).TryCoerceValue("99999999999999999999", out var coerced).ShouldBeFalse();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_NullableDecimalType_CoercesCorrectly()
    {
        typeof(decimal?).TryCoerceValue("200", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(200m);
    }

    [Fact]
    public void TryCoerceValue_NullableIntTypeWithNull_ReturnsTrueAndNull()
    {
        typeof(int?).TryCoerceValue(null, out var coerced).ShouldBeTrue();
        coerced.ShouldBeNull();
    }

    [Fact]
    public void TryCoerceValue_LongString_CoercesCorrectly()
    {
        typeof(long).TryCoerceValue("9223372036854775807", out var coerced).ShouldBeTrue();
        coerced.ShouldBe(9223372036854775807L);
    }

    #endregion

    #region Expression-overload coercion parity

    [Fact]
    public async Task ExpressionOverload_DynamicAnd_WithStringForDecimalCoercesCorrectly()
    {
        Expression<Func<Product, bool>> predicate = p => p.IsActive;

        var result = predicate.DynamicAnd("Price", Ops.GreaterThan, "100");

        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.IsActive && p.Price > 100m);
    }

    [Fact]
    public async Task ExpressionOverload_DynamicAnd_WithUnconvertibleStringReturnsUnchanged()
    {
        Expression<Func<Product, bool>> predicate = p => p.IsActive;

        var result = predicate.DynamicAnd("Price", Ops.GreaterThan, "abc");

        var products = await _context.Products.AsExpandable().Where(result).ToListAsync();
        products.ShouldAllBe(p => p.IsActive);
    }

    #endregion

    #region Helpers

    private static string NormalizeSql(string sql)
        => sql
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);

    #endregion
}
