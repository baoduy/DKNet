// <copyright file="EntityConfigExtensionInfoTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Reflection;
using DKNet.EfCore.Extensions.Internal;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Regression coverage for C14: <see cref="EntityConfigExtensionInfo" /> used to return a constant service
///     provider hash and an always-true <c>ShouldUseSameServiceProvider</c>, so two <c>DbContext</c>s configured
///     with different assembly sets via <c>UseAutoConfigModel(...)</c> could share EF Core's cached model. Both
///     members must now vary with the extension's assembly set, order-independently.
/// </summary>
public class EntityConfigExtensionInfoTests
{
    #region Methods

    [Fact]
    public void GetServiceProviderHashCode_WithDifferentAssemblySets_ReturnsDifferentHashes()
    {
        var register1 = new EntityAutoConfigRegister([typeof(User).Assembly]);
        var register2 = new EntityAutoConfigRegister([typeof(object).Assembly]);

        register1.Info.GetServiceProviderHashCode().ShouldNotBe(register2.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void GetServiceProviderHashCode_WithSameAssembliesInDifferentOrder_ReturnsSameHash()
    {
        var assemblies = new[] { typeof(User).Assembly, typeof(object).Assembly };
        var register1 = new EntityAutoConfigRegister(assemblies);
        var register2 = new EntityAutoConfigRegister(assemblies.Reverse().ToArray());

        register1.Info.GetServiceProviderHashCode().ShouldBe(register2.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void ShouldUseSameServiceProvider_WithDifferentAssemblySets_ReturnsFalse()
    {
        var register1 = new EntityAutoConfigRegister([typeof(User).Assembly]);
        var register2 = new EntityAutoConfigRegister([typeof(object).Assembly]);

        register1.Info.ShouldUseSameServiceProvider(register2.Info).ShouldBeFalse();
    }

    [Fact]
    public void ShouldUseSameServiceProvider_WithSameAssemblySetInDifferentOrder_ReturnsTrue()
    {
        var assemblies = new[] { typeof(User).Assembly, typeof(object).Assembly };
        var register1 = new EntityAutoConfigRegister(assemblies);
        var register2 = new EntityAutoConfigRegister(assemblies.Reverse().ToArray());

        register1.Info.ShouldUseSameServiceProvider(register2.Info).ShouldBeTrue();
    }

    /// <summary>
    ///     DRK-1969 R1: the global model builder set drives the built model, so two registers with the same
    ///     assemblies but different builder sets must never share an internal service provider (and its model).
    /// </summary>
    [Fact]
    public void DifferentBuilderSets_DoNotShareProvider()
    {
        Assembly[] assemblies = [typeof(User).Assembly];
        var withoutBuilders = new EntityAutoConfigRegister(assemblies, []);
        var withBuilder = new EntityAutoConfigRegister(assemblies, [typeof(TestGlobalQueryFilter)]);

        withoutBuilders.Info.GetServiceProviderHashCode().ShouldNotBe(withBuilder.Info.GetServiceProviderHashCode());
        withoutBuilders.Info.ShouldUseSameServiceProvider(withBuilder.Info).ShouldBeFalse();
    }

    /// <summary>
    ///     DRK-1969 R2: the builder set is compared as a set - order and duplicates (repeated
    ///     <c>AddDataOwnerProvider</c> calls) must not split the provider.
    /// </summary>
    [Fact]
    public void SameBuilderSetAnyOrderOrDuplicates_SharesProvider()
    {
        Assembly[] assemblies = [typeof(User).Assembly];
        var register1 = new EntityAutoConfigRegister(
            assemblies,
            [typeof(TestGlobalQueryFilter), typeof(PassingFilter)]);
        var register2 = new EntityAutoConfigRegister(
            assemblies,
            [typeof(PassingFilter), typeof(TestGlobalQueryFilter), typeof(TestGlobalQueryFilter)]);

        register1.Info.GetServiceProviderHashCode().ShouldBe(register2.Info.GetServiceProviderHashCode());
        register1.Info.ShouldUseSameServiceProvider(register2.Info).ShouldBeTrue();
    }

    #endregion
}
