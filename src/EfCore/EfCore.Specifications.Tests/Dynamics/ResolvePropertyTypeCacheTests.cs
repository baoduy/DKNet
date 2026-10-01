// <copyright file="ResolvePropertyTypeCacheTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Dynamics;

namespace EfCore.Specifications.Tests.Dynamics;

/// <summary>
///     Pins what <see cref="DynamicPredicateBuilderExtensions.ResolvePropertyType" /> keeps in its process-wide
///     cache. Property paths reach it from caller input (list-endpoint filters), so the cache may hold only real
///     properties — one entry each, whatever casing the caller used — or any caller can grow it without bound.
///     The cache is shared by every test running in parallel, so each test resolves against a probe type of its
///     own and counts only that type's entries.
/// </summary>
public class ResolvePropertyTypeCacheTests
{
    #region Methods

    [Fact]
    public void ResolvePropertyType_UnknownPath_StoresNothing()
    {
        var result = typeof(MissProbe).ResolvePropertyType("NoSuch");

        result.ShouldBeNull();
        DynamicPredicateBuilderExtensions.PropertyTypeCache.Keys
            .Count(k => k.EntityType == typeof(MissProbe)).ShouldBe(0);
    }

    [Fact]
    public void ResolvePropertyType_UnknownNestedPath_StoresNothing()
    {
        var result = typeof(NestedMissProbe).ResolvePropertyType("Name.NoSuch");

        result.ShouldBeNull();
        DynamicPredicateBuilderExtensions.PropertyTypeCache.Keys
            .Count(k => k.EntityType == typeof(NestedMissProbe)).ShouldBe(0);
    }

    [Fact]
    public void ResolvePropertyType_TwoCasingsOfOneProperty_ShareOneCacheEntry()
    {
        var first = typeof(CasingProbe).ResolvePropertyType("NAmE");
        var second = typeof(CasingProbe).ResolvePropertyType("NaMe");

        first.ShouldBe(typeof(string));
        second.ShouldBe(typeof(string));
        DynamicPredicateBuilderExtensions.PropertyTypeCache.Keys
            .Count(k => k.EntityType == typeof(CasingProbe)).ShouldBe(1);
    }

    #endregion

    private sealed class MissProbe
    {
        #region Properties

        public string Name { get; set; } = string.Empty;

        #endregion
    }

    private sealed class NestedMissProbe
    {
        #region Properties

        public string Name { get; set; } = string.Empty;

        #endregion
    }

    private sealed class CasingProbe
    {
        #region Properties

        public string Name { get; set; } = string.Empty;

        #endregion
    }
}
