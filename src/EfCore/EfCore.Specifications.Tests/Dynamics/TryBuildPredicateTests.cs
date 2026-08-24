// <copyright file="TryBuildPredicateTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Dynamics;
using EfCore.Specifications.Tests.TestEntities;

namespace EfCore.Specifications.Tests.Dynamics;

/// <summary>
///     Covers <see cref="DynamicPredicateExtensions.TryBuildPredicate{T}" /> — the explicit-failure entry point
///     HTTP endpoints use, where <see langword="false" /> becomes a 400 and an exception would surface as a 500.
///     Because callers hand it raw query-string input, every unusable input must come back as
///     <see langword="false" />, never as a throw.
/// </summary>
public class TryBuildPredicateTests
{
    #region Methods

    [Fact]
    public void TryBuildPredicate_ValidStringFilter_ReturnsThePredicate()
    {
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Product>(
            "Name", Ops.Contains, "phone", out var predicate);

        ok.ShouldBeTrue();
        predicate.ShouldNotBeNull();
    }

    [Fact]
    public void TryBuildPredicate_ReferenceNavigationProperty_ReturnsFalseInsteadOfThrowing()
    {
        // "Category" resolves on Product but is a complex type, so the generated clause compares an object to
        // a string. Dynamic LINQ rejects that at parse time — and an uncaught ParseException here surfaces as
        // a 500 for a caller who merely filtered on a nested field of the model (filter=category:Equal:x).
        // The contract is the same as every other unusable input: report false, let the endpoint answer 400.
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Product>(
            "Category", Ops.Equal, "x", out var predicate);

        ok.ShouldBeFalse();
        predicate.ShouldBeNull();
    }

    [Fact]
    public void TryBuildPredicate_CollectionNavigationProperty_ReturnsFalse()
    {
        // A collection navigation fails at the same parse step as the reference kind — pinned separately so
        // the two navigation shapes cannot drift apart.
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Order>(
            "OrderItems", Ops.Equal, "x", out var predicate);

        ok.ShouldBeFalse();
        predicate.ShouldBeNull();
    }

    [Fact]
    public void TryBuildPredicate_UnknownProperty_ReturnsFalse()
    {
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Product>(
            "NoSuchField", Ops.Equal, "x", out var predicate);

        ok.ShouldBeFalse();
        predicate.ShouldBeNull();
    }

    [Fact]
    public void TryBuildPredicate_IsNullIgnoresTheValue_ReturnsThePredicate()
    {
        // The wire format always carries a value segment, so whatever arrives there must not disturb an
        // operation that has no use for it.
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Order>(
            "CustomerName", Ops.IsNull, "ignored", out var predicate);

        ok.ShouldBeTrue();
        predicate.ShouldNotBeNull();
    }

    [Fact]
    public void TryBuildPredicate_IsNullOnAnEnumProperty_ReturnsThePredicate()
    {
        // The pipeline's enum validation rejects any value that does not convert to the enum — which is every
        // value, for an operation that ignores its value. IsNull/IsNotNull must bypass value validation
        // entirely or an enum column can never be null-checked.
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Order>(
            "Status", Ops.IsNull, string.Empty, out var predicate);

        ok.ShouldBeTrue();
        predicate.ShouldNotBeNull();
    }

    [Fact]
    public void TryBuildPredicate_IsNotNullOnUnknownProperty_StillReturnsFalse()
    {
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Order>(
            "NoSuchField", Ops.IsNotNull, string.Empty, out var predicate);

        ok.ShouldBeFalse();
        predicate.ShouldBeNull();
    }

    [Fact]
    public void TryBuildPredicate_UnconvertibleValue_ReturnsFalse()
    {
        var ok = DynamicPredicateExtensions.TryBuildPredicate<Product>(
            "Price", Ops.Equal, "not-a-number", out var predicate);

        ok.ShouldBeFalse();
        predicate.ShouldBeNull();
    }

    #endregion
}
